using System.Collections.Generic;

namespace ReferenceCopAnalyzer
{
    public struct ReferenceRule
    {
        public readonly bool IsNegated;
        public readonly string RuleTarget;
        public readonly string RuleSourceRegex;
        public readonly IReadOnlyList<string> ReplacedSourcePatterns;

        public ReferenceRule(bool isNegated, string ruleSourceRegex, string ruleTarget, IReadOnlyList<string> replacedSourcePatterns)
        {
            IsNegated = isNegated;
            RuleSourceRegex = ruleSourceRegex;
            RuleTarget = ruleTarget;
            ReplacedSourcePatterns = replacedSourcePatterns;
        }
    }
}