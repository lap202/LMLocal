using System.Collections.Generic;
using Newtonsoft.Json;

namespace LMLocal.Infrastructure.Lm.Responses
{
    internal class ListModelsResponse
    {
        [JsonProperty("models")]
        public List<ModelInfo> Models { get; set; }
    }

    internal class ModelInfo
    {
        [JsonProperty("type")]
        public string Type { get; set; } // "llm" | "embedding"

        [JsonProperty("publisher")]
        public string Publisher { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("architecture", NullValueHandling = NullValueHandling.Ignore)]
        public string Architecture { get; set; }

        [JsonProperty("quantization")]
        public QuantizationInfo Quantization { get; set; }

        [JsonProperty("size_bytes")]
        public long SizeBytes { get; set; } // use long to accommodate large models

        [JsonProperty("params_string")]
        public string ParamsString { get; set; }

        [JsonProperty("loaded_instances")]
        public List<LoadedInstance> LoadedInstances { get; set; }

        [JsonProperty("max_context_length")]
        public int MaxContextLength { get; set; }

        [JsonProperty("format")]
        public string Format { get; set; } // "gguf" | "mlx" | null

        [JsonProperty("capabilities", NullValueHandling = NullValueHandling.Ignore)]
        public ModelCapabilities Capabilities { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }
    }

    internal class QuantizationInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("bits_per_weight")]
        public double? BitsPerWeight { get; set; }
    }

    internal class LoadedInstance
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("config")]
        public InstanceConfig Config { get; set; }
    }

    internal class InstanceConfig
    {
        [JsonProperty("context_length")]
        public int ContextLength { get; set; }

        [JsonProperty("eval_batch_size", NullValueHandling = NullValueHandling.Ignore)]
        public int? EvalBatchSize { get; set; }

        [JsonProperty("flash_attention", NullValueHandling = NullValueHandling.Ignore)]
        public bool? FlashAttention { get; set; }

        [JsonProperty("num_experts", NullValueHandling = NullValueHandling.Ignore)]
        public int? NumExperts { get; set; }

        [JsonProperty("offload_kv_cache_to_gpu", NullValueHandling = NullValueHandling.Ignore)]
        public bool? OffloadKvCacheToGpu { get; set; }
    }

    internal class ModelCapabilities
    {
        [JsonProperty("vision")]
        public bool Vision { get; set; }

        [JsonProperty("trained_for_tool_use")]
        public bool TrainedForToolUse { get; set; }
    }
}


