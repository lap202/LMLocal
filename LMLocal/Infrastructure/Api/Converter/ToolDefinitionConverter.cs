using System.Collections.Generic;
using System.Linq;
using LMLocal.Infrastructure.Api.Requests;
using VsToolDefinition = LMLocal.Infrastructure.Vs.ToolDefinition;
using VsToolDetails = LMLocal.Infrastructure.Vs.ToolDetails;
using VsToolParameters = LMLocal.Infrastructure.Vs.ToolParameters;

namespace LMLocal.Infrastructure.Api
{
    /// <summary>
    /// Converts tool definitions from VS internal format to OpenAI Chat Completions API format.
    /// </summary>
    internal static class ToolDefinitionConverter
    {
        /// <summary>
        /// Converts a list of VS tool definitions to OpenAI tool definitions format.
        /// </summary>
        public static List<ToolDefinition> ConvertToOpenAiFormat(
            IReadOnlyList<VsToolDefinition> vsTools)
        {
            if (vsTools == null || vsTools.Count == 0)
                return new List<ToolDefinition>();

            return vsTools
                .Select(ConvertSingleTool)
                .ToList();
        }

        private static ToolDefinition ConvertSingleTool(VsToolDefinition vsTool)
        {
            var functionDef = new FunctionDefinition
            {
                Name = vsTool.Name,
                Description = vsTool.Description,
                Parameters = ConvertParameters(vsTool.Parameters)
            };

            return new ToolDefinition
            {
                Type = "function",
                Function = functionDef
            };
        }

        private static FunctionParameters ConvertParameters(VsToolParameters vsParams)
        {
            if (vsParams == null)
            {
                return new FunctionParameters
                {
                    Type = "object",
                    Properties = new Dictionary<string, object>(),
                    Required = new List<string>()
                };
            }

            var properties = ConvertProperties(vsParams.Properties);

            return new FunctionParameters
            {
                Type = vsParams.Type ?? "object",
                Properties = properties,
                Required = vsParams.Required
            };
        }

        private static Dictionary<string, object> ConvertProperties(
            Dictionary<string, VsToolDetails> vsProperties)
        {
            if (vsProperties == null || vsProperties.Count == 0)
                return new Dictionary<string, object>();

            var result = new Dictionary<string, object>();

            foreach (var kvp in vsProperties)
            {
                var toolDetail = kvp.Value;
                var propertyObj = new Dictionary<string, object>
                {
                    { "type", toolDetail.Type },
                    { "description", toolDetail.Description }
                };

                result[kvp.Key] = propertyObj;
            }

            return result;
        }
    }
}
