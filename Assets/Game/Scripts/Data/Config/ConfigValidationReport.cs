/* 配置校验结果只收集问题，不在校验阶段修改配置资产。 */
using System.Collections.Generic;

namespace SlimeBattle.Data
{
    public sealed class ConfigValidationReport
    {
        private readonly List<string> errors = new List<string>();

        public IReadOnlyList<string> Errors => errors.AsReadOnly();

        public int ErrorCount => errors.Count;

        public bool IsValid => errors.Count == 0;

        internal void AddError(string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                errors.Add(error);
            }
        }
    }
}
