// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime.Implementation;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class DACNameParserTests
    {
        [Fact]
        public void NullReturnsNull()
        {
            Assert.Null(DacNameParser.Parse(null));
        }

        [Fact]
        public void EmptyStringReturnsOriginalString()
        {
            Assert.Same(string.Empty, DacNameParser.Parse(string.Empty));
        }

        [Fact]
        public void ParseSimpleTypeNameReturnsOriginalString()
        {
            string input = "System.String";

            Assert.Same(input, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseSimpleTypeNameWithNestedTypeReturnsOriginalString()
        {
            string input = "Test.Foo+Bar";

            Assert.Same(input, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseArrayTypeReturnsOriginalString()
        {
            string input = "System.String[]";

            Assert.Same(input, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseMultidimensionalArrayTypeReturnsOriginalString()
        {
            string input = "System.String[,,,]";

            Assert.Same(input, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseMultidimensionalArrayTypeReturnsOriginalString2()
        {
            string input = "System.String[][][]";

            Assert.Same(input, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseArrayOfArrayOfType()
        {
            string input = "System.Collections.Generic.IList`1[[JetBrains.ReSharper.Psi.Files.CommitBuildResults, JetBrains.ReSharper.Psi]][][]";
            string expected = "System.Collections.Generic.IList<JetBrains.ReSharper.Psi.Files.CommitBuildResults>[][]";

            Assert.Equal(expected, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseArrayOfArrayOfArrayOfType()
        {
            string input = "System.Collections.Generic.IList`1[[JetBrains.ReSharper.Psi.Files.CommitBuildResults, JetBrains.ReSharper.Psi]][][][]";
            string expected = "System.Collections.Generic.IList<JetBrains.ReSharper.Psi.Files.CommitBuildResults>[][][]";

            Assert.Equal(expected, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseArrayOfArrayOfArrayOfArrayOfType()
        {
            string input = "System.Collections.Generic.IList`1[[JetBrains.ReSharper.Psi.Files.CommitBuildResults, JetBrains.ReSharper.Psi]][][][][]";
            string expected = "System.Collections.Generic.IList<JetBrains.ReSharper.Psi.Files.CommitBuildResults>[][][][]";

            Assert.Equal(expected, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseArrayOfArrayOfArrayOfArrayOfArrayOfType()
        {
            string input = "System.Collections.Generic.IList`1[[JetBrains.ReSharper.Psi.Files.CommitBuildResults, JetBrains.ReSharper.Psi]][][][][][]";
            string expected = "System.Collections.Generic.IList<JetBrains.ReSharper.Psi.Files.CommitBuildResults>[][][][][]";

            Assert.Equal(expected, DacNameParser.Parse(input));
        }


        [Fact]
        public void ParseGenericWithMissingArgs()
        {
            string input = "Test.Foo`2";
            string expectedResult = "Test.Foo<T1, T2>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseGenericWithNestedClass()
        {
            string input = "System.Lazy`1+Boxed[[Microsoft.CodeAnalysis.Options.IDocumentOptionsProviderFactory, Microsoft.CodeAnalysis.Workspaces]]";
            string expectedResult = "System.Lazy<Microsoft.CodeAnalysis.Options.IDocumentOptionsProviderFactory>+Boxed";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseGenericWithGenericNestedClass()
        {
            string input = "System.Collections.Generic.Dictionary`2[[Microsoft.VisualStudio.Threading.WeakKeyDictionary`2+WeakReference`1[[Microsoft.VisualStudio.Threading.IJoinableTaskDependent, Microsoft.VisualStudio.Threading],[System.Int32, mscorlib],[Microsoft.VisualStudio.Threading.IJoinableTaskDependent, Microsoft.VisualStudio.Threading]], Microsoft.VisualStudio.Threading],[System.Int32, mscorlib]]";
            string expectedResult = "System.Collections.Generic.Dictionary<Microsoft.VisualStudio.Threading.WeakKeyDictionary<Microsoft.VisualStudio.Threading.IJoinableTaskDependent, System.Int32>+WeakReference<Microsoft.VisualStudio.Threading.IJoinableTaskDependent>, System.Int32>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseManyNestedClasses()
        {
            string input = "Microsoft.CodeAnalysis.PooledObjects.ObjectPool`1[[System.Collections.Generic.Stack`1[[System.ValueTuple`2[[Microsoft.CodeAnalysis.Shared.Collections.IntervalTree`1+Node[[Microsoft.CodeAnalysis.Editor.Shared.Tagging.TagSpanIntervalTree`1+TagNode[[Microsoft.VisualStudio.Text.Tagging.IErrorTag, Microsoft.VisualStudio.Text.UI]], Microsoft.CodeAnalysis.EditorFeatures]], Microsoft.CodeAnalysis.Workspaces],[System.Boolean, mscorlib]], mscorlib]], System]]";
            string expectedResult = "Microsoft.CodeAnalysis.PooledObjects.ObjectPool<System.Collections.Generic.Stack<System.ValueTuple<Microsoft.CodeAnalysis.Shared.Collections.IntervalTree<Microsoft.CodeAnalysis.Editor.Shared.Tagging.TagSpanIntervalTree<Microsoft.VisualStudio.Text.Tagging.IErrorTag>+TagNode>+Node, System.Boolean>>>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));

        }

        [Fact]
        public void ParseNestedClassWithNameContainingAngleBrackets()
        {
            string input = "Microsoft.VisualStudio.Text.Tagging.Implementation.TagAggregator`1+<<-ctor>b__16_0>d[[Microsoft.VisualStudio.Text.Editor.ISuggestionTag, Microsoft.VisualStudio.Text.Internal]]";
            string expectedResult = "Microsoft.VisualStudio.Text.Tagging.Implementation.TagAggregator<Microsoft.VisualStudio.Text.Editor.ISuggestionTag>+<<-ctor>b__16_0>d";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }


        [Fact]
        public void ParseNestedClassWithNestedClassWithNameContainingAngleBrackets()
        {
            string input = "JetBrains.Threading.Actor`1+<>c__DisplayClass15_0+<<-ctor>b__0>d[[System.Boolean, mscorlib]]";
            string expectedResult = "JetBrains.Threading.Actor<System.Boolean>+<>c__DisplayClass15_0+<<-ctor>b__0>d";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseGenericClassWithNestedGenericClassWithNameContainingAngleBrackets()
        {
            string input = "JetBrains.ProjectModel.Caches.ProjectFileDataCacheBase`1+<>c__DisplayClass9_0`1[[Newtonsoft.Json.Linq.JObject, Newtonsoft.Json],[System.Collections.Generic.IList`1[[JetBrains.Util.Dotnet.TargetFrameworkIds.TargetFrameworkId, JetBrains.Platform.Util]], mscorlib]]";
            string expectedResult = "JetBrains.ProjectModel.Caches.ProjectFileDataCacheBase<Newtonsoft.Json.Linq.JObject>+<>c__DisplayClass9_0<System.Collections.Generic.IList<JetBrains.Util.Dotnet.TargetFrameworkIds.TargetFrameworkId>>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseNestedGenericWithNestedGenericArg()
        {
            string input = "Microsoft.SqlServer.Management.SqlParser.Binder.ExecutionSimulator`1+NodeBase`1[T,[Microsoft.SqlServer.Management.SqlParser.Binder.ExecutionSimulator`1+ExecutionNode, Microsoft.SqlServer.Management.SqlParser]]";
            string expectedResult = "Microsoft.SqlServer.Management.SqlParser.Binder.ExecutionSimulator<T>+NodeBase<Microsoft.SqlServer.Management.SqlParser.Binder.ExecutionSimulator<T1>+ExecutionNode>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseGenericWithComplexGenericArgs()
        {
            string input = "System.Func`2[[Microsoft.CodeAnalysis.UseAutoProperty.AbstractUseAutoPropertyAnalyzer`4+AnalysisResult[[Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax, Microsoft.CodeAnalysis.CSharp],[Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax, Microsoft.CodeAnalysis.CSharp],[Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax, Microsoft.CodeAnalysis.CSharp],[Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax, Microsoft.CodeAnalysis.CSharp]], Microsoft.CodeAnalysis.Features],[System.ValueTuple`2[[Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax, Microsoft.CodeAnalysis.CSharp],[Microsoft.CodeAnalysis.SemanticModel, Microsoft.CodeAnalysis]], mscorlib]]";
            string expectedResult = "System.Func<Microsoft.CodeAnalysis.UseAutoProperty.AbstractUseAutoPropertyAnalyzer<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax, Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax, Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax, Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax>+AnalysisResult, System.ValueTuple<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax, Microsoft.CodeAnalysis.SemanticModel>>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseGenericWithComplexGenericArgs2()
        {
            string input = "System.Func`3[[System.Collections.Generic.KeyValuePair`2[[Microsoft.VisualStudio.ProjectSystem.IProjectVersionedValue`1[[System.Tuple`3[[Microsoft.VisualStudio.ProjectSystem.IProjectSnapshotWithCapabilities, Microsoft.VisualStudio.ProjectSystem],[System.Collections.Immutable.IImmutableDictionary`2[[System.String, mscorlib],[System.String, mscorlib]], System.Collections.Immutable],[System.Boolean, mscorlib]], mscorlib]], Microsoft.VisualStudio.ProjectSystem],[Microsoft.VisualStudio.ProjectSystem.IProjectVersionedValue`1[[Microsoft.VisualStudio.ProjectSystem.Build.IProjectBuildSnapshot, Microsoft.VisualStudio.ProjectSystem]], Microsoft.VisualStudio.ProjectSystem]], mscorlib],[Microsoft.VisualStudio.ProjectSystem.Designers.CustomizableBlockSubscriberBase`3+Subscription[[Microsoft.VisualStudio.ProjectSystem.IProjectVersionedValue`1[[System.Tuple`3[[Microsoft.VisualStudio.ProjectSystem.IProjectSnapshotWithCapabilities, Microsoft.VisualStudio.ProjectSystem],[System.Collections.Immutable.IImmutableDictionary`2[[System.String, mscorlib],[System.String, mscorlib]], System.Collections.Immutable],[System.Boolean, mscorlib]], mscorlib]], Microsoft.VisualStudio.ProjectSystem],[Microsoft.VisualStudio.ProjectSystem.Build.IProjectBuildSnapshot, Microsoft.VisualStudio.ProjectSystem],[Microsoft.VisualStudio.ProjectSystem.Designers.ProjectBuildSnapshotSubscription, Microsoft.VisualStudio.ProjectSystem.Implementation]], Microsoft.VisualStudio.ProjectSystem.Implementation],[System.Collections.Generic.KeyValuePair`2[[Microsoft.VisualStudio.ProjectSystem.IProjectVersionedValue`1[[Microsoft.VisualStudio.ProjectSystem.Build.IProjectBuildSnapshot, Microsoft.VisualStudio.ProjectSystem]], Microsoft.VisualStudio.ProjectSystem],[System.Boolean, mscorlib]], mscorlib]]";
            string expectedResult = "System.Func<System.Collections.Generic.KeyValuePair<Microsoft.VisualStudio.ProjectSystem.IProjectVersionedValue<System.Tuple<Microsoft.VisualStudio.ProjectSystem.IProjectSnapshotWithCapabilities, System.Collections.Immutable.IImmutableDictionary<System.String, System.String>, System.Boolean>>, Microsoft.VisualStudio.ProjectSystem.IProjectVersionedValue<Microsoft.VisualStudio.ProjectSystem.Build.IProjectBuildSnapshot>>, Microsoft.VisualStudio.ProjectSystem.Designers.CustomizableBlockSubscriberBase<Microsoft.VisualStudio.ProjectSystem.IProjectVersionedValue<System.Tuple<Microsoft.VisualStudio.ProjectSystem.IProjectSnapshotWithCapabilities, System.Collections.Immutable.IImmutableDictionary<System.String, System.String>, System.Boolean>>, Microsoft.VisualStudio.ProjectSystem.Build.IProjectBuildSnapshot, Microsoft.VisualStudio.ProjectSystem.Designers.ProjectBuildSnapshotSubscription>+Subscription, System.Collections.Generic.KeyValuePair<Microsoft.VisualStudio.ProjectSystem.IProjectVersionedValue<Microsoft.VisualStudio.ProjectSystem.Build.IProjectBuildSnapshot>, System.Boolean>>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseArrayOfNestedGenericType()
        {
            string input = "Microsoft.TeamFoundation.Git.Contracts.PathTable`1+PathTableRow`1[[System.String, mscorlib],[System.String, mscorlib]][]";
            string expectedResult = "Microsoft.TeamFoundation.Git.Contracts.PathTable<System.String>+PathTableRow<System.String>[]";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        // Parse a generic arg list with some assembly-qualified names, and some non-qualified names, moving the location of the assembly-qualified arg throughout the list
        // in the various tests (i.e. ParseValueTupleWithoutAllFullyQualifiedNames1, ParseValueTupleWithoutAllFullyQualifiedNames2, ...)
        [Fact]
        public void ParseValueTupleWithoutAllFullyQualifiedNames1()
        {
            string input = "System.ValueTuple`3[[System.String, mscorlib],TCheapVersion,TExpensiveVersion]";
            string expectedResult = "System.ValueTuple<System.String, TCheapVersion, TExpensiveVersion>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseValueTupleWithoutAllFullyQualifiedNames2()
        {
            string input = "System.ValueTuple`3[TCheapVersion,[System.String, mscorlib],TExpensiveVersion]";
            string expectedResult = "System.ValueTuple<TCheapVersion, System.String, TExpensiveVersion>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseValueTupleWithoutAllFullyQualifiedNames3()
        {
            string input = "System.ValueTuple`3[TCheapVersion,TExpensiveVersion,[System.String, mscorlib]]";
            string expectedResult = "System.ValueTuple<TCheapVersion, TExpensiveVersion, System.String>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        // Test with NO qualified names
        [Fact]
        public void ParseValueTupleWithNoFullyQualifiedNames()
        {
            string input = "System.ValueTuple`3[TType1,TType2,TType3]";
            string expectedResult = "System.ValueTuple<TType1, TType2, TType3>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void Bug897()
        {
            // https://github.com/microsoft/clrmd/issues/897

            // This input is unusual both in that T and TPluginType are not assembly qualified, but also LambdaInstance is a non-closed generic type.
            string input = "StructureMap.Pipeline.ExpressedInstance`3[[StructureMap.Pipeline.LambdaInstance`2, EnforceHttpsModule],T,TPluginType]";
            string expectedResult = "StructureMap.Pipeline.ExpressedInstance<StructureMap.Pipeline.LambdaInstance<T1, T2>, T, TPluginType>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        // Some simple variants on Bug897
        [Fact]
        public void Bug897_Variant1()
        {
            string input = "StructureMap.Pipeline.ExpressedInstance`3[[StructureMap.Pipeline.LambdaInstance`2[[System.String, mscorlib],SomeFakeType], SomeFakeAssembly],T,TPluginType]";
            string expectedResult = "StructureMap.Pipeline.ExpressedInstance<StructureMap.Pipeline.LambdaInstance<System.String, SomeFakeType>, T, TPluginType>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void Bug897_Variant2()
        {
            string input = "StructureMap.Pipeline.ExpressedInstance`3[[StructureMap.Pipeline.LambdaInstance`2[[List`1[[System.ValueType`2[[System.Boolean, mscorlib],SomeOtherFakeType], mscorlib]], mscorlib],SomeFakeType], SomeFakeAssembly],T,TPluginType]";
            string expectedResult = "StructureMap.Pipeline.ExpressedInstance<StructureMap.Pipeline.LambdaInstance<List<System.ValueType<System.Boolean, SomeOtherFakeType>>, SomeFakeType>, T, TPluginType>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseOuterMostGenericWithFinalArgumentBeingAssemblyQualifiedNestedGeneric()
        {
            string input = "System.Linq.Parallel.QueryOperatorEnumerator`2[TSource,[System.Linq.Parallel.ConcatKey`2[TLeftKey,TRightKey], System.Core]]";
            string expectedResult = "System.Linq.Parallel.QueryOperatorEnumerator<TSource, System.Linq.Parallel.ConcatKey<TLeftKey, TRightKey>>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseGenericWithAssemblyQualifiedArrayTypeParam()
        {
            string input = "System.Collections.Generic.Dictionary`2[[System.String, mscorlib],[System.Object[], mscorlib]]";
            string expectedResult = "System.Collections.Generic.Dictionary<System.String, System.Object[]>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseGenericWithNonAssemblyQualifiedArrayTypeParam()
        {
            string input = "System.Collections.Generic.Dictionary`2[[System.String, mscorlib],TFoo[]]";
            string expectedResult = "System.Collections.Generic.Dictionary<System.String, TFoo[]>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseGenericWithAssemblyQualifiedArrayOfArrayTypeParam()
        {
            string input = "System.Collections.Generic.Dictionary`2[[System.String, mscorlib],[System.Object[][], mscorlib]]";
            string expectedResult = "System.Collections.Generic.Dictionary<System.String, System.Object[][]>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseGenericWithNonAssemblyQualifiedArrayOfArrayTypeParam()
        {
            string input = "System.Collections.Generic.Dictionary`2[[System.String, mscorlib],TFoo[][]]";
            string expectedResult = "System.Collections.Generic.Dictionary<System.String, TFoo[][]>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseGenericWithAssemblyQualifiedMultiDimensionalArrayTypeParam()
        {
            string input = "System.Collections.Generic.Dictionary`2[[System.String, mscorlib],[System.Object[,,], mscorlib]]";
            string expectedResult = "System.Collections.Generic.Dictionary<System.String, System.Object[,,]>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseGenericWithNonAssemblyQualifiedMultiDimensionalArrayTypeParam()
        {
            string input = "System.Collections.Generic.Dictionary`2[[System.String, mscorlib],TFoo[,,]]";
            string expectedResult = "System.Collections.Generic.Dictionary<System.String, TFoo[,,]>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseArrayOfNestedGenericTypeWithNonAssemblyQualifiedArrayTypeParam()
        {
            string input = "System.Collections.Generic.Dictionary`2+Entry[[System.Int32, mscorlib],[System.Collections.Generic.Stack`1[[System.Type[], mscorlib]], System]][]";
            string expectedResult = "System.Collections.Generic.Dictionary<System.Int32, System.Collections.Generic.Stack<System.Type[]>>+Entry[]";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void ParseNestedGenericAnonymousTypes()
        {
            string input = "System.Func`2[[<>f__AnonymousType12`2[[<>f__AnonymousType10`2[[System.Reflection.ConstructorInfo, mscorlib],[System.Reflection.ParameterInfo[], mscorlib]], Microsoft.VisualStudio.Composition],[System.Reflection.TypeInfo, mscorlib]], Microsoft.VisualStudio.Composition],[System.Reflection.ConstructorInfo, mscorlib]]";
            string expectedResult = "System.Func<<>f__AnonymousType12<<>f__AnonymousType10<System.Reflection.ConstructorInfo, System.Reflection.ParameterInfo[]>, System.Reflection.TypeInfo>, System.Reflection.ConstructorInfo>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void HandlesNonTraditionalGenericTypeMissingAritySpecifier()
        {
            // NOTE: jaredpar in the Roslyn compiler team verified this is in fact a generic type in F#, but it has no arity specifier. We should be able to handle
            // these by recognizing when we encounter the [ and decide we are parsing an array specifier (which we should since we never saw a generic arity specifier)
            // if we see anything other than a ] or a , after it (i.e. say a letter), then we can assume this is one of these non-traditional generic cases and manually force
            // ourself down that path. It means we will need to figure out the arity ourselves, but that shouldn't be terribly hard, we just start at 1 and count any commas
            // we encounter before the closing ] (taking care for nested generics inside the outer generic in our counting).
            string input = "FSharp.Compiler.TypedTreeBasics+loop@444-39T[a]";
            string expectedResult = "FSharp.Compiler.TypedTreeBasics+loop@444-39T<a>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void HandlesNonTraditionalGenericTypeWithMultipleParamsMissingAritySpecifier()
        {
            string input = "Microsoft.FSharp.Collections.ArrayModule+Parallel+sortingFunc@2439-1[TKey,TValue]";
            string expectedResult = "Microsoft.FSharp.Collections.ArrayModule+Parallel+sortingFunc@2439-1<TKey, TValue>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }


        [Fact]
        public void HandlesNonTraditionalGenericTypeWithNestedGenericTypeMissingAritySpecifier()
        {
            string input = "Microsoft.FSharp.Collections.ArrayModule+Parallel+sortingFunc@2439-1[TKey,[TSomeFoo, TFakeAssembly]]";
            string expectedResult = "Microsoft.FSharp.Collections.ArrayModule+Parallel+sortingFunc@2439-1<TKey, TSomeFoo>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void HandlesNonTraditionalGenericTypeWithArrayParamMissingAritySpecifier()
        {
            string input = "Microsoft.FSharp.Collections.ArrayModule+Parallel+sortingFunc@2439-1[TKey[]]";
            string expectedResult = "Microsoft.FSharp.Collections.ArrayModule+Parallel+sortingFunc@2439-1<TKey[]>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void HandleNonTraditionObfuscatedGenericWithAssemblyQualifiedArg()
        {
            string input = "a37[[akx, yWorks.yFilesWPF.Viewer]]";
            string expectedResult = "a37<akx>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }

        [Fact]
        public void HandleNonTraditionObfuscatedGenericWithNonAssemblyQualifiedArg()
        {
            string input = "a37[akx]";
            string expectedResult = "a37<akx>";

            Assert.Equal(expectedResult, DacNameParser.Parse(input));
        }
    }
}