using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = ReferenceCopAnalyzer.Test.Verifiers.CSharpAnalyzerVerifier<ReferenceCopAnalyzer.ReferenceCopAnalyzer>;

namespace ReferenceCopAnalyzer.Test
{
    public class VariousReferencesTests
    {
        private const string VariableDeclaration = @"
namespace One {
    class Source {
        public Source() {
            Two.SourceTwo a;
        }
    }
}
namespace Two {
    class SourceTwo {}
}
";

        private const string New = @"
namespace One {
    class Source {
        public Source() {
            var a = new Two.SourceTwo();
        }
    }
}
namespace Two {
    class SourceTwo {}
}
";

        private const string StaticImport = @"
namespace One {
    using static Two.SourceTwo;
    class Source {
        public Source() {
            Method();
        }
    }
}
namespace Two {
    public static class SourceTwo {
        public static void Method() {}
    }
}
";
        private const string AliasForClassImport = @"
namespace One {
    using ST=Two.SourceTwo;
    class Source {
        public Source() {
            ST s = new();
        }
    }
}
namespace Two {
    public class SourceTwo {}
}
";
        private const string TypeOf = @"
namespace One {
    class Source {
        public Source() {
            var x = typeof(Two.SourceTwo);
        }
    }
}
namespace Two {
    public class SourceTwo {}
}
";

        private const string NameOfType = @"
namespace One {
    class Source {
        public Source() {
            var x = nameof(Two.SourceTwo);
        }
    }
}
namespace Two {
    public class SourceTwo {}
}
";

        private const string NameOfNamespace = @"
namespace One {
    class Source {
        public Source() {
            var x = nameof(Two);
        }
    }
}
namespace Two {
    public class SourceTwo {}
}
";

        private const string Attribute = @"
namespace One {
    [Two.Attr]
    class Source {
    }
}
namespace Two {
    public class Attr : System.Attribute {}
}
";

        private const string Enum = @"
namespace One {
    class Source {
        private Two.NumNum n;
    }
}
namespace Two {
    public enum NumNum {
        O, T
    }
}
";

        private const string GenericTypeArg = @"
namespace One {
    class Source<T> {}
    class SourceGen : Source<Two.Gen> {}
}
namespace Two {
    public class Gen {}
}
";

        [Theory]
        [InlineData(VariableDeclaration, "Two One", 5, 13, 5, 26, new[] { "One", "Two" })]
        [InlineData(New, "Two One", 5, 25, 5, 38, new[] { "One", "Two" })]
        [InlineData(StaticImport, "Two One", 3, 5, 3, 32, new[] { "One", "Two" })]
        [InlineData(AliasForClassImport, "Two One", 3, 5, 3, 28, new[] { "One", "Two" })]
        [InlineData(TypeOf, "Two One", 5, 28, 5, 41, new[] { "One", "Two" })]
        [InlineData(NameOfType, "Two One", 5, 21, 5, 42, new[] { "One", "Two" })]
        [InlineData(NameOfNamespace, "Two One", 5, 21, 5, 32, new[] { "One", "Two" })]
        [InlineData(Attribute, @"Two System
Two One", 3, 6, 3, 14, new[] { "One", "Two" })]
        [InlineData(Enum, "Two One", 4, 17, 4, 27, new[] { "One", "Two" })]
        [InlineData(GenericTypeArg, "Two One", 4, 30, 4, 37, new[] { "One", "Two" })]
        public async Task ShouldReportIllegalReference(
            string source,
            string rules,
            int startLine,
            int startColumn,
            int endLine,
            int endColumn,
            object[] arguments)
        {
            await VerifyCS.VerifyReferenceCopAnalysis(source, rules, new[]
                {
                    VerifyCS
                        .Diagnostic(ReferenceCopAnalyzer.ReferenceNotAllowedDiagnosticId)
                        .WithSpan(startLine, startColumn, endLine, endColumn)
                        .WithArguments(arguments)
                });
        }

        [Theory]
        [InlineData(VariableDeclaration, "One Two")]
        [InlineData(New, "One Two")]
        [InlineData(StaticImport, "One Two")]
        [InlineData(AliasForClassImport, "One Two")]
        [InlineData(TypeOf, "One Two")]
        [InlineData(NameOfType, "One Two")]
        [InlineData(NameOfNamespace, "One Two")]
        [InlineData(Attribute, @"Two System
One Two")]
        [InlineData(Enum, "One Two")]
        [InlineData(GenericTypeArg, "One Two")]
        public async Task ShouldNotReportIllegalReference(string source, string rules)
        {
            await VerifyCS.VerifyReferenceCopAnalysis(source, rules, Array.Empty<DiagnosticResult>());
        }
    }
}
