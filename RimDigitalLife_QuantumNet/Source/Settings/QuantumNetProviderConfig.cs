using Verse;

namespace RimDigitalLife_QuantumNet
{
    // 单个 AI 供应商配置（照 RimPay / RimTuber 模式，独立实现）
    public class QuantumNetProviderConfig : IExposable
    {
        public string label = "";
        public QuantumNetProvider provider = QuantumNetProvider.SiliconFlow;
        public string endpointUrl = "";
        public string apiKey = "";
        public string model = "";
        public bool enabled = false;

        public QuantumNetProviderConfig()
        {
        }

        public QuantumNetProviderConfig(QuantumNetProvider provider, string label = "", string endpointUrl = "", string apiKey = "", string model = "", bool enabled = true)
        {
            this.provider = provider;
            this.label = string.IsNullOrEmpty(label) ? provider.GetLabel() : label;
            this.endpointUrl = string.IsNullOrEmpty(endpointUrl) ? (provider.GetEndpointUrl() ?? "") : endpointUrl;
            this.apiKey = apiKey ?? "";
            this.model = string.IsNullOrEmpty(model) ? (provider.GetDefaultModel() ?? "") : model;
            this.enabled = enabled;
        }

        public string GetEffectiveEndpoint()
        {
            if (!string.IsNullOrEmpty(endpointUrl))
                return endpointUrl;
            return provider.GetEndpointUrl() ?? "";
        }

        public string GetEffectiveModel()
        {
            if (!string.IsNullOrEmpty(model))
                return model;
            return provider.GetDefaultModel() ?? "";
        }

        public string GetEffectiveKey()
        {
            return apiKey ?? "";
        }

        public string DisplayLabel
        {
            get
            {
                string pLabel = provider.GetLabel();
                string mLabel = GetEffectiveModel();
                if (!string.IsNullOrEmpty(mLabel) && mLabel != pLabel)
                    return $"{pLabel} ({mLabel})";
                return pLabel;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref label, "label", "");
            string pStr = provider.ToString();
            Scribe_Values.Look(ref pStr, "provider", "SiliconFlow");
            provider = QuantumNetProviderRegistry.FromString(pStr);
            Scribe_Values.Look(ref endpointUrl, "endpointUrl", "");
            Scribe_Values.Look(ref apiKey, "apiKey", "");
            Scribe_Values.Look(ref model, "model", "");
            Scribe_Values.Look(ref enabled, "enabled", false);
        }

        public QuantumNetProviderConfig Copy()
        {
            return new QuantumNetProviderConfig
            {
                label = label,
                provider = provider,
                endpointUrl = endpointUrl,
                apiKey = apiKey,
                model = model,
                enabled = enabled
            };
        }
    }
}
