using System;
using System.Reflection;
using RimWorld;
using Verse;

namespace RimDigitalLife
{
    public enum RimTalkIntegrationMode
    {
        None,
        ViaQueue,
        ViaInteractionDef
    }

    public static class RimTalkBridge
    {
        private static bool _initialized = false;
        private static bool _queueAvailable = false;
        private static bool _isRimTalkAvailableCache = false;
        
        private static Type _cacheType;
        private static Type _talkTypeType;
        
        private static MethodInfo _cacheGetMethod;
        private static MethodInfo _addTalkRequestMethod;
        
        private static object _talkTypeUser;
        private static object _talkTypeOther;

        private static int _lastRequestTick = 0;
        private const int MinRequestInterval = 30;

        public static RimTalkIntegrationMode IntegrationMode
        {
            get
            {
                if (!_initialized) Initialize();
                
                if (_queueAvailable) return RimTalkIntegrationMode.ViaQueue;
                if (IsRimTalkModActive()) return RimTalkIntegrationMode.ViaInteractionDef;
                return RimTalkIntegrationMode.None;
            }
        }

        public static bool IsRimTalkAvailable
        {
            get { return _isRimTalkAvailableCache; }
        }

        private static bool IsRimTalkModActive()
        {
            return ModLister.GetActiveModWithIdentifier("cj.rimtalk") != null ||
                   ModLister.GetActiveModWithIdentifier("CJ.RimTalk") != null ||
                   ModLister.GetActiveModWithIdentifier("jlibrary.rimtalk") != null;
        }

        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            if (!IsRimTalkModActive())
            {
                Log.Message("[RimDigitalLife] RimTalk mod not detected");
                _isRimTalkAvailableCache = false;
                return;
            }

            try
            {
                var rimTalkAssembly = Assembly.Load("RimTalk");
                if (rimTalkAssembly == null)
                {
                    Log.Message("[RimDigitalLife] RimTalk assembly not found");
                    _isRimTalkAvailableCache = false;
                    return;
                }

                _cacheType = rimTalkAssembly.GetType("RimTalk.Data.Cache");
                _talkTypeType = rimTalkAssembly.GetType("RimTalk.Source.Data.TalkType");
                var pawnStateType = rimTalkAssembly.GetType("RimTalk.Data.PawnState");

                if (_talkTypeType != null)
                {
                    foreach (var val in Enum.GetValues(_talkTypeType))
                    {
                        string name = val.ToString();
                        if (name == "User") _talkTypeUser = val;
                        else if (name == "Other") _talkTypeOther = val;
                    }
                }
                
                if (_cacheType != null)
                {
                    _cacheGetMethod = _cacheType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(Pawn) }, null);
                }
                
                if (pawnStateType != null)
                {
                    _addTalkRequestMethod = pawnStateType.GetMethod("AddTalkRequest", BindingFlags.Public | BindingFlags.Instance);
                }

                if (_cacheGetMethod != null && _addTalkRequestMethod != null && _talkTypeUser != null)
                {
                    _queueAvailable = true;
                    _isRimTalkAvailableCache = true;
                    Log.Message("[RimDigitalLife] RimTalk queue bridge initialized successfully");
                }
                else
                {
                    _isRimTalkAvailableCache = IsRimTalkModActive();
                    Log.Message("[RimDigitalLife] RimTalk queue API not fully available, using InteractionDef fallback");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimDigitalLife] RimTalk init failed: {ex.Message}");
                _isRimTalkAvailableCache = false;
            }
        }

        public static bool TriggerDialogue(Pawn initiator, Pawn recipient, string systemPrompt, string userPrompt, bool isDirectQuestion, AssistantPersona persona)
        {
            if (!IsRimTalkAvailable)
            {
                Log.Warning("[RimDigitalLife] RimTalk not available");
                return false;
            }

            if (_queueAvailable)
            {
                return TriggerDialogueViaQueue(initiator, persona);
            }
            else
            {
                string defName = GetInteractionDefName(persona, isDirectQuestion);
                return TriggerDialogueViaInteractionDef(initiator, defName);
            }
        }

        private static string GetInteractionDefName(AssistantPersona persona, bool isDirectQuestion)
        {
            if (persona == AssistantPersona.Rimi)
                return isDirectQuestion ? "SmartAssistant_Ask_Rimi" : "SmartAssistant_Rimi";
            else
                return isDirectQuestion ? "SmartAssistant_Ask_Sirim" : "SmartAssistant_Sirim";
        }

        private static bool TriggerDialogueViaQueue(Pawn initiator, AssistantPersona persona)
        {
            try
            {
                int currentTick = GenTicks.TicksGame;
                if (currentTick - _lastRequestTick < MinRequestInterval)
                {
                    Log.Message("[RimDigitalLife] Request rate limited, queueing anyway");
                }

                string assistantName = persona == AssistantPersona.Rimi ? "Rimi" : "Sirim";
                string assistantDesc = persona == AssistantPersona.Rimi 
                    ? "活泼可爱、喜欢用表情符号和语气词" 
                    : "冷静理性、说话精准简洁";
                string otherName = persona == AssistantPersona.Rimi ? "Sirim" : "Rimi";
                string exampleStyle = persona == AssistantPersona.Rimi 
                    ? "哇！Rimi发消息说东边那堆玻璃钢成分特别纯，适合做防护！~" 
                    : "Sirim分析结果显示该区域资源分布不均，建议优先采集东边...";

                string prompt = $"【重要】本次对话的AI助手是【{assistantName}】，不是{otherName}！\n\n" +
                                $"{initiator.LabelShort}刚刚在手机上收到了AI助手【{assistantName}】的消息。\n" +
                                $"{assistantName}是这个手机上的AI助手，性格{assistantDesc}。\n" +
                                $"现在{initiator.LabelShort}要说出自己收到【{assistantName}】消息后的反应。\n\n" +
                                $"严格要求：对话中只能说\"{assistantName}\"这个名字，绝对不能说成{otherName}或iShen。\n" +
                                $"示例：\"{exampleStyle}\"";

                var pawnState = _cacheGetMethod.Invoke(null, new object[] { initiator });
                if (pawnState == null)
                {
                    Log.Warning("[RimDigitalLife] Failed to get PawnState");
                    return false;
                }

                Pawn recipient = FindNearbyColonist(initiator) ?? initiator;
                
                _addTalkRequestMethod.Invoke(pawnState, new object[] { prompt, recipient, _talkTypeUser });
                
                _lastRequestTick = currentTick;
                Log.Message($"[RimDigitalLife] Added talk request to queue for {initiator.LabelShort} with {assistantName}, recipient: {recipient.LabelShort}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimDigitalLife] Queue trigger failed: {ex}");
                return false;
            }
        }

        private static Pawn FindNearbyColonist(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null) return null;

            Pawn closestPawn = null;
            float closestDist = 999999f;

            foreach (Pawn otherPawn in pawn.Map.mapPawns.AllPawns)
            {
                if (otherPawn == pawn) continue;

                if (otherPawn.Faction == Faction.OfPlayer &&
                    otherPawn.RaceProps.Humanlike &&
                    !otherPawn.Dead &&
                    otherPawn.Spawned &&
                    otherPawn.Awake())
                {
                    float dist = pawn.Position.DistanceTo(otherPawn.Position);
                    if (dist < closestDist && dist <= 50f)
                    {
                        closestDist = dist;
                        closestPawn = otherPawn;
                    }
                }
            }

            return closestPawn;
        }

        private static bool TriggerDialogueViaInteractionDef(Pawn initiator, string defName)
        {
            try
            {
                InteractionDef intDef = DefDatabase<InteractionDef>.GetNamedSilentFail(defName);
                if (intDef == null)
                {
                    Log.Warning($"[RimDigitalLife] InteractionDef '{defName}' not found");
                    return false;
                }

                PlayLogEntry_Interaction entry = new PlayLogEntry_Interaction(intDef, initiator, initiator, null);
                Find.PlayLog.Add(entry);
                Log.Message($"[RimDigitalLife] Added InteractionDef log entry");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimDigitalLife] InteractionDef trigger failed: {ex}");
                return false;
            }
        }

        public static string GetRimiSystemPrompt(bool isDirectQuestion)
        {
            return "";
        }

        public static string GetSirimSystemPrompt(bool isDirectQuestion)
        {
            return "";
        }

        public static string GetUserPromptForPawn(Pawn pawn, bool isDirectQuestion)
        {
            if (pawn == null) return "general advice";
            
            float moodLevel = pawn.needs?.mood?.CurLevelPercentage ?? 0.5f;
            string moodDesc = moodLevel > 0.7f ? "心情不错" : moodLevel > 0.4f ? "心情一般" : "心情不好";
            string doingWhat = pawn.CurJob?.def?.label ?? "闲着";

            if (isDirectQuestion)
            {
                return $"{pawn.LabelShort}({moodDesc}，正在{doingWhat})想问问AI助手有什么建议";
            }
            else
            {
                return $"给{pawn.LabelShort}({moodDesc}，正在{doingWhat})发个提醒";
            }
        }
    }
}
