using System;
using System.Collections.Generic;

namespace RimDigitalLife_RimPay
{
    public enum RimPayProvider
    {
        Google,
        OpenAI,
        DeepSeek,
        Grok,
        GLM,
        OpenRouter,
        SiliconFlow,
        Player2,
        Local,
        Custom,
        None
    }

    public struct ProviderDef
    {
        public string Label;
        public string EndpointUrl;
        public string DefaultModel;
        public bool RequiresApiKey;
    }

    public static class RimPayProviderRegistry
    {
        // Player2 桌面端本地服务：免费使用 AI，无 API Key 需求
        public const string Player2GameClientId = "019e12ba-6062-79ae-8c76-de13bea9af7a";
        public const string Player2LocalUrl = "http://127.0.0.1:4315";

        public static readonly Dictionary<RimPayProvider, ProviderDef> Defs = new Dictionary<RimPayProvider, ProviderDef>
        {
            {
                RimPayProvider.Google, new ProviderDef
                {
                    Label = "Google Gemini",
                    EndpointUrl = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
                    DefaultModel = "gemini-2.0-flash",
                    RequiresApiKey = true
                }
            },
            {
                RimPayProvider.OpenAI, new ProviderDef
                {
                    Label = "OpenAI",
                    EndpointUrl = "https://api.openai.com/v1/chat/completions",
                    DefaultModel = "gpt-4o-mini",
                    RequiresApiKey = true
                }
            },
            {
                RimPayProvider.DeepSeek, new ProviderDef
                {
                    Label = "DeepSeek",
                    EndpointUrl = "https://api.deepseek.com/v1/chat/completions",
                    DefaultModel = "deepseek-chat",
                    RequiresApiKey = true
                }
            },
            {
                RimPayProvider.Grok, new ProviderDef
                {
                    Label = "Grok (xAI)",
                    EndpointUrl = "https://api.x.ai/v1/chat/completions",
                    DefaultModel = "grok-3-mini",
                    RequiresApiKey = true
                }
            },
            {
                RimPayProvider.GLM, new ProviderDef
                {
                    Label = "GLM (Zhipu)",
                    EndpointUrl = "https://api.z.ai/api/paas/v4/chat/completions",
                    DefaultModel = "glm-4-flash",
                    RequiresApiKey = true
                }
            },
            {
                RimPayProvider.OpenRouter, new ProviderDef
                {
                    Label = "OpenRouter",
                    EndpointUrl = "https://openrouter.ai/api/v1/chat/completions",
                    DefaultModel = "openai/gpt-4o-mini",
                    RequiresApiKey = true
                }
            },
            {
                RimPayProvider.SiliconFlow, new ProviderDef
                {
                    Label = "SiliconFlow",
                    EndpointUrl = "https://api.siliconflow.cn/v1/chat/completions",
                    DefaultModel = "",
                    RequiresApiKey = true
                }
            },
            {
                RimPayProvider.Player2, new ProviderDef
                {
                    Label = "Player2",
                    EndpointUrl = "http://127.0.0.1:4315/v1/chat/completions",
                    DefaultModel = "Default",
                    RequiresApiKey = false
                }
            },
            {
                RimPayProvider.Local, new ProviderDef
                {
                    Label = "本地模型 (Ollama / LM Studio)",
                    EndpointUrl = "http://127.0.0.1:11434/v1/chat/completions",
                    DefaultModel = "qwen2.5:7b",
                    RequiresApiKey = false
                }
            },
            {
                RimPayProvider.Custom, new ProviderDef
                {
                    Label = "Custom",
                    EndpointUrl = "",
                    DefaultModel = "",
                    RequiresApiKey = true
                }
            }
        };

        public static string GetLabel(this RimPayProvider p)
        {
            if (Defs.TryGetValue(p, out var def) && !string.IsNullOrEmpty(def.Label))
                return def.Label;
            return p.ToString();
        }

        public static string GetEndpointUrl(this RimPayProvider p)
        {
            if (p == RimPayProvider.Player2)
                return Player2LocalUrl + "/v1/chat/completions";
            return Defs.TryGetValue(p, out var def) ? def.EndpointUrl : null;
        }

        public static string GetDefaultModel(this RimPayProvider p)
        {
            return Defs.TryGetValue(p, out var def) ? def.DefaultModel : "";
        }

        public static bool GetRequiresApiKey(this RimPayProvider p)
        {
            return Defs.TryGetValue(p, out var def) && def.RequiresApiKey;
        }

        public static RimPayProvider FromString(string s)
        {
            if (string.IsNullOrEmpty(s)) return RimPayProvider.None;
            if (Enum.TryParse<RimPayProvider>(s, true, out var result)) return result;
            return RimPayProvider.None;
        }
    }
}