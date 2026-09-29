using System;
using System.Collections.Generic;

namespace RimDigitalLife_QuantumNet
{
    public enum QuantumNetProvider
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

    public struct QuantumNetProviderDef
    {
        public string Label;
        public string EndpointUrl;
        public string DefaultModel;
        public bool RequiresApiKey;
    }

    public static class QuantumNetProviderRegistry
    {
        // Player2 桌面端本地服务：免费使用 AI，无 API Key 需求（与 RimPay 共用同一客户端）
        public const string Player2GameClientId = "019e12ba-6062-79ae-8c76-de13bea9af7a";
        public const string Player2LocalUrl = "http://127.0.0.1:4315";

        public static readonly Dictionary<QuantumNetProvider, QuantumNetProviderDef> Defs = new Dictionary<QuantumNetProvider, QuantumNetProviderDef>
        {
            {
                QuantumNetProvider.Google, new QuantumNetProviderDef
                {
                    Label = "Google Gemini",
                    EndpointUrl = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
                    DefaultModel = "gemini-2.0-flash",
                    RequiresApiKey = true
                }
            },
            {
                QuantumNetProvider.OpenAI, new QuantumNetProviderDef
                {
                    Label = "OpenAI",
                    EndpointUrl = "https://api.openai.com/v1/chat/completions",
                    DefaultModel = "gpt-4o-mini",
                    RequiresApiKey = true
                }
            },
            {
                QuantumNetProvider.DeepSeek, new QuantumNetProviderDef
                {
                    Label = "DeepSeek",
                    EndpointUrl = "https://api.deepseek.com/v1/chat/completions",
                    DefaultModel = "deepseek-chat",
                    RequiresApiKey = true
                }
            },
            {
                QuantumNetProvider.Grok, new QuantumNetProviderDef
                {
                    Label = "Grok (xAI)",
                    EndpointUrl = "https://api.x.ai/v1/chat/completions",
                    DefaultModel = "grok-3-mini",
                    RequiresApiKey = true
                }
            },
            {
                QuantumNetProvider.GLM, new QuantumNetProviderDef
                {
                    Label = "GLM (Zhipu)",
                    EndpointUrl = "https://api.z.ai/api/paas/v4/chat/completions",
                    DefaultModel = "glm-4-flash",
                    RequiresApiKey = true
                }
            },
            {
                QuantumNetProvider.OpenRouter, new QuantumNetProviderDef
                {
                    Label = "OpenRouter",
                    EndpointUrl = "https://openrouter.ai/api/v1/chat/completions",
                    DefaultModel = "openai/gpt-4o-mini",
                    RequiresApiKey = true
                }
            },
            {
                QuantumNetProvider.SiliconFlow, new QuantumNetProviderDef
                {
                    Label = "SiliconFlow",
                    EndpointUrl = "https://api.siliconflow.cn/v1/chat/completions",
                    DefaultModel = "",
                    RequiresApiKey = true
                }
            },
            {
                QuantumNetProvider.Player2, new QuantumNetProviderDef
                {
                    Label = "Player2",
                    EndpointUrl = "http://127.0.0.1:4315/v1/chat/completions",
                    DefaultModel = "Default",
                    RequiresApiKey = false
                }
            },
            {
                QuantumNetProvider.Local, new QuantumNetProviderDef
                {
                    Label = "本地模型 (Ollama / LM Studio)",
                    EndpointUrl = "http://127.0.0.1:11434/v1/chat/completions",
                    DefaultModel = "qwen2.5:7b",
                    RequiresApiKey = false
                }
            },
            {
                QuantumNetProvider.Custom, new QuantumNetProviderDef
                {
                    Label = "Custom",
                    EndpointUrl = "",
                    DefaultModel = "",
                    RequiresApiKey = true
                }
            }
        };

        public static string GetLabel(this QuantumNetProvider p)
        {
            if (Defs.TryGetValue(p, out var def) && !string.IsNullOrEmpty(def.Label))
                return def.Label;
            return p.ToString();
        }

        public static string GetEndpointUrl(this QuantumNetProvider p)
        {
            if (p == QuantumNetProvider.Player2)
                return Player2LocalUrl + "/v1/chat/completions";
            return Defs.TryGetValue(p, out var def) ? def.EndpointUrl : null;
        }

        public static string GetDefaultModel(this QuantumNetProvider p)
        {
            return Defs.TryGetValue(p, out var def) ? def.DefaultModel : "";
        }

        public static bool GetRequiresApiKey(this QuantumNetProvider p)
        {
            return Defs.TryGetValue(p, out var def) && def.RequiresApiKey;
        }

        public static QuantumNetProvider FromString(string s)
        {
            if (string.IsNullOrEmpty(s)) return QuantumNetProvider.None;
            if (Enum.TryParse<QuantumNetProvider>(s, true, out var result)) return result;
            return QuantumNetProvider.None;
        }
    }
}
