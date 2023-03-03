// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.Implementation;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class DACNameParserTests
    {
        [Fact]
        public void NullReturnsNull()
        {
            Assert.Null(DACNameParser.Parse(null));
        }

        [Fact]
        public void EmptyStringReturnsOriginalString()
        {
            Assert.Same(string.Empty, DACNameParser.Parse(string.Empty));
        }

        [Fact]
        public void ParseSimpleTypeNameReturnsOriginalString()
        {
            string input = "System.String";

            Assert.Same(input, DACNameParser.Parse(input));
        }

        [Fact]
        public void ParseSimpleTypeNameWithNestedTypeReturnsOriginalString()
        {
            string input = "Test.Foo+Bar";

            Assert.Same(input, DACNameParser.Parse(input));
        }

        [Fact]
        public void ParseArrayTypeReturnsOriginalString()
        {
            string input = "System.String[]";

            Assert.Same(input, DACNameParser.Parse(input));
        }

        [Fact]
        public void ParseMultidimensionalArrayTypeReturnsOriginalString()
        {
            string input = "System.String[,,,]";

            Assert.Same(input, DACNameParser.Parse(input));
        }

        [Fact]
        public void ParseMultidimensionalArrayTypeReturnsOriginalString2()
        {
            string input = "System.String[][][]";

            Assert.Same(input, DACNameParser.Parse(input));
        }

        [Fact]
        public void ParseArrayOfArrayOfType()
        {
            string input = "System.Collections.Generic.IList`1[[JetBrains.ReSharper.Psi.Files.CommitBuildResults, JetBrains.ReSharper.Psi]][][]";
            string expected = "System.Collections.Generic.IList<JetBrains.ReSharper.Psi.Files.CommitBuildResults>[][]";

            Assert.Equal(expected, DACNameParser.Parse(input));
        }

        [Fact]
        public void ParseArrayOfArrayOfArrayOfType()
        {
            string input = "System.Collections.Generic.IList`1[[JetBrains.ReSharper.Psi.Files.CommitBuildResults, JetBrains.ReSharper.Psi]][][][]";
            string expected = "System.Collections.Generic.IList<JetBrains.ReSharper.Psi.Files.CommitBuildResults>[][][]";

            Assert.Equal(expected, DACNameParser.Parse(input));
        }

        [Fact]
        public void ParseArrayOfArrayOfArrayOfArrayOfType()
        {
            string input = "System.Collections.Generic.IList`1[[JetBrains.ReSharper.Psi.Files.CommitBuildResults, JetBrains.ReSharper.Psi]][][][][]";
            string expected = "System.Collections.Generic.IList<JetBrains.ReSharper.Psi.Files.CommitBuildResults>[][][][]";

            Assert.Equal(expected, DACNameParser.Parse(input));
        }

        [Fact]
        public void ParseArrayOfArrayOfArrayOfArrayOfArrayOfType()
        {
            string input = "System.Collections.Generic.IList`1[[JetBrains.ReSharper.Psi.Files.CommitBuildResults, JetBrains.ReSharper.Psi]][][][][][]";
            string expected = "System.Collections.Generic.IList<JetBrains.ReSharper.Psi.Files.CommitBuildResults>[][][][][]";

            Assert.Equal(expected, DACNameParser.Parse(input));
        }


        [Fact]
        public void ParseGenericWithMissingArgs()
        {
            string input = "Test.Foo`2";
            string expectedResult = "Test.Foo<T1, T2>";

            Assert.Equal(expectedResult, DACNameParser.Parse(input));
        }

        [Fact]
        public void ParseGenericWithNestedClass()
        {
            string input = "System.Lazy`1+Boxed[[Microsoft.CodeAnalysis.Options.IDocumentOptionsProviderFactory, Microsoft.CodeAnalysis.Workspaces]]";
            string expectedResult = "System.Lazy<Microsoft.CodeAnalysis.Options.IDocumentOptionsProviderFactory>+Boxed";

            Assert.Equal(expectedResult, DACNameParser.Parse(input));
        }

        [Fact]
        public void ParseGenericWithGenericNestedClass()
        {
            string input = "System.Collections.Generic.Dictionary`2[[Microsoft.VisualStudio.Threading.WeakKeyDictionary`2+WeakReference`1[[Microsoft.VisualStudio.Threading.IJoinableTaskDependent, Microsoft.VisualStudio.Threading],[System.Int32, mscorlib],[Microsoft.VisualStudio.Threading.IJoinableTaskDependent, Microsoft.VisualStudio.Threading]], Microsoft.VisualStudio.Threading],[System.Int32, mscorlib]]";
            string expectedResult = "System.Collections.Generic.Dictionary<Microsoft.VisualStudio.Threading.WeakKeyDictionary<Microsoft.VisualStudio.Threading.IJoinableTaskDependent, System.Int32>+WeakReference<Microsoft.VisualStudio.Threading.IJoinableTaskDependent>, System.Int32>";

            Assert.Equal(expectedResult, DACNameParser.Parse(input));
        }

        [Fact]
        public void ParseManyNestedClasses()
        {
            string input = "Microsoft.CodeAnalysis.PooledObjects.ObjectPool`1[[System.Collections.Generic.Stack`1[[System.ValueTuple`2[[Microsoft.CodeAnalysis.Shared.Collections.IntervalTree`1+Node[[Microsoft.CodeAnalysis.Editor.Shared.Tagging.TagSpanIntervalTree`1+TagNode[[Microsoft.VisualStudio.Text.Tagging.IErrorTag, Microsoft.VisualStudio.Text.UI]], Microsoft.CodeAnalysis.EditorFeatures]], Microsoft.CodeAnalysis.Workspaces],[System.Boolean, mscorlib]], mscorlib]], System]]";
            string expectedResult = "Microsoft.CodeAnalysis.PooledObjects.ObjectPool<System.Collections.Generic.Stack<System.ValueTuple<Microsoft.CodeAnalysis.Shared.Collections.IntervalTree<Microsoft.CodeAnalysis.Editor.Shared.Tagging.TagSpanIntervalTree<Microsoft.VisualStudio.Text.Tagging.IErrorTag>+TagNode>+Node, System.Boolean>>>";

            Assert.Equal(expectedResult, DACNameParser.Parse(input));

        }

        [Fact]
        public void ParseNestedClassWithNameContainingAngleBrackets()
        {
            string input = "Microsoft.VisualStudio.Text.Tagging.Implementation.TagAggregator`1+<<-ctor>b__16_0>d[[Microsoft.VisualStudio.Text.Editor.ISuggestionTag, Microsoft.VisualStudio.Text.Internal]]";
            string expectedResult = "Microsoft.VisualStudio.Text.Tagging.Implementation.TagAggregator<Microsoft.VisualStudio.Text.Editor.ISuggestionTag>+<<-ctor>b__16_0>d";

            Assert.Equal(expectedResult, DACNameParser.Parse(input));
        }


        [Fact]
        public void ParseNestedClassWithNestedClassWithNameContainingAngleBrackets()
        {
            string input = "JetBrains.Threading.Actor`1+<>c__DisplayClass15_0+<<-ctor>b__0>d[[System.Boolean, mscorlib]]";
            string expectedResult = "JetBrains.Threading.Actor<System.Boolean>+<>c__DisplayClass15_0+<<-ctor>b__0>d";

            Assert.Equal(expectedResult, DACNameParser.Parse(input));
        }

        [Fact]
        public void ParseGenericClassWithNestedGenericClassWithNameContainingAngleBrackets()
        {
            string input = "JetBrains.ProjectModel.Caches.ProjectFileDataCacheBase`1+<>c__DisplayClass9_0`1[[Newtonsoft.Json.Linq.JObject, Newtonsoft.Json],[System.Collections.Generic.IList`1[[JetBrains.Util.Dotnet.TargetFrameworkIds.TargetFrameworkId, JetBrains.Platform.Util]], mscorlib]]";
            string expectedResult = "JetBrains.ProjectModel.Caches.ProjectFileDataCacheBase<Newtonsoft.Json.Linq.JObject>+<>c__DisplayClass9_0<System.Collections.Generic.IList<JetBrains.Util.Dotnet.TargetFrameworkIds.TargetFrameworkId>>";

            Assert.Equal(expectedResult, DACNameParser.Parse(input));
        }

        [Fact]
        public void ParseGenericWithComplexGenericArgs()
        {
            string input = "System.Func`2[[Microsoft.CodeAnalysis.UseAutoProperty.AbstractUseAutoPropertyAnalyzer`4+AnalysisResult[[Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax, Microsoft.CodeAnalysis.CSharp],[Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax, Microsoft.CodeAnalysis.CSharp],[Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax, Microsoft.CodeAnalysis.CSharp],[Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax, Microsoft.CodeAnalysis.CSharp]], Microsoft.CodeAnalysis.Features],[System.ValueTuple`2[[Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax, Microsoft.CodeAnalysis.CSharp],[Microsoft.CodeAnalysis.SemanticModel, Microsoft.CodeAnalysis]], mscorlib]]";
            string expectedResult = "System.Func<Microsoft.CodeAnalysis.UseAutoProperty.AbstractUseAutoPropertyAnalyzer<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax, Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax, Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax, Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax>+AnalysisResult, System.ValueTuple<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax, Microsoft.CodeAnalysis.SemanticModel>>";

            Assert.Equal(expectedResult, DACNameParser.Parse(input));
        }

        [Fact]
        public void ParseGenericWithComplexGenericArgs2()
        {
            string input = "System.Func`3[[System.Collections.Generic.KeyValuePair`2[[Microsoft.VisualStudio.ProjectSystem.IProjectVersionedValue`1[[System.Tuple`3[[Microsoft.VisualStudio.ProjectSystem.IProjectSnapshotWithCapabilities, Microsoft.VisualStudio.ProjectSystem],[System.Collections.Immutable.IImmutableDictionary`2[[System.String, mscorlib],[System.String, mscorlib]], System.Collections.Immutable],[System.Boolean, mscorlib]], mscorlib]], Microsoft.VisualStudio.ProjectSystem],[Microsoft.VisualStudio.ProjectSystem.IProjectVersionedValue`1[[Microsoft.VisualStudio.ProjectSystem.Build.IProjectBuildSnapshot, Microsoft.VisualStudio.ProjectSystem]], Microsoft.VisualStudio.ProjectSystem]], mscorlib],[Microsoft.VisualStudio.ProjectSystem.Designers.CustomizableBlockSubscriberBase`3+Subscription[[Microsoft.VisualStudio.ProjectSystem.IProjectVersionedValue`1[[System.Tuple`3[[Microsoft.VisualStudio.ProjectSystem.IProjectSnapshotWithCapabilities, Microsoft.VisualStudio.ProjectSystem],[System.Collections.Immutable.IImmutableDictionary`2[[System.String, mscorlib],[System.String, mscorlib]], System.Collections.Immutable],[System.Boolean, mscorlib]], mscorlib]], Microsoft.VisualStudio.ProjectSystem],[Microsoft.VisualStudio.ProjectSystem.Build.IProjectBuildSnapshot, Microsoft.VisualStudio.ProjectSystem],[Microsoft.VisualStudio.ProjectSystem.Designers.ProjectBuildSnapshotSubscription, Microsoft.VisualStudio.ProjectSystem.Implementation]], Microsoft.VisualStudio.ProjectSystem.Implementation],[System.Collections.Generic.KeyValuePair`2[[Microsoft.VisualStudio.ProjectSystem.IProjectVersionedValue`1[[Microsoft.VisualStudio.ProjectSystem.Build.IProjectBuildSnapshot, Microsoft.VisualStudio.ProjectSystem]], Microsoft.VisualStudio.ProjectSystem],[System.Boolean, mscorlib]], mscorlib]]";
            string expectedResult = "System.Func<System.Collections.Generic.KeyValuePair<Microsoft.VisualStudio.ProjectSystem.IProjectVersionedValue<System.Tuple<Microsoft.VisualStudio.ProjectSystem.IProjectSnapshotWithCapabilities, System.Collections.Immutable.IImmutableDictionary<System.String, System.String>, System.Boolean>>, Microsoft.VisualStudio.ProjectSystem.IProjectVersionedValue<Microsoft.VisualStudio.ProjectSystem.Build.IProjectBuildSnapshot>>, Microsoft.VisualStudio.ProjectSystem.Designers.CustomizableBlockSubscriberBase<Microsoft.VisualStudio.ProjectSystem.IProjectVersionedValue<System.Tuple<Microsoft.VisualStudio.ProjectSystem.IProjectSnapshotWithCapabilities, System.Collections.Immutable.IImmutableDictionary<System.String, System.String>, System.Boolean>>, Microsoft.VisualStudio.ProjectSystem.Build.IProjectBuildSnapshot, Microsoft.VisualStudio.ProjectSystem.Designers.ProjectBuildSnapshotSubscription>+Subscription, System.Collections.Generic.KeyValuePair<Microsoft.VisualStudio.ProjectSystem.IProjectVersionedValue<Microsoft.VisualStudio.ProjectSystem.Build.IProjectBuildSnapshot>, System.Boolean>>";

            Assert.Equal(expectedResult, DACNameParser.Parse(input));
        }

        [Fact]
        public void ParseArrayOfNestedGenericType()
        {
            string input = "Microsoft.TeamFoundation.Git.Contracts.PathTable`1+PathTableRow`1[[System.String, mscorlib],[System.String, mscorlib]][]";
            string expectedResult = "Microsoft.TeamFoundation.Git.Contracts.PathTable<System.String>+PathTableRow<System.String>[]";
                
            Assert.Equal(expectedResult, DACNameParser.Parse(input));
        }
    }
}