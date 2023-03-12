// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    // This class handles stateless, non-recursive parsing of DAC names, converting things like this:
    //
    // System.Collections.Generic.HashSet`1+Slot[[Roslyn.Utilities.ReferenceHolder`1[[Microsoft.CodeAnalysis.ISymbol, Microsoft.CodeAnalysis]], Microsoft.CodeAnalysis.Workspaces]][]
    //
    // to this:
    //
    // System.Collections.Generic.HashSet<Roslyn.Utilities.ReferenceHolder<Microsoft.CodeAnalysis.ISymbol>>+Slot[]
    //
    // Besides being stateless (and thus thread safe by default) its design emphasizes the following:
    //
    // 1) Minimal heap allocation.
    // 2) Fast, non-recursive parsing.
    // 3) Accuracy in the face of complex nesting of types, nesting of generic args, etc...
    // 4) Resiliancy to failure (i.e. returns the original DAC name if errors are encountered instead of throwing).
    internal static class DACNameParser
    {
        private const char GenericAritySpecifier = '`';
        private const char ArgSeperator = ',';
        private const char NestedClassSpecifier = '+';
        private const char GenericArgListOrArrayStartSpecifier = '[';
        private const char GenericArgListOrArrayEndSpecifier = ']';

        [return: NotNullIfNotNull(nameof(name))]
        public static string? Parse(string? name)
        {
            if (name == null)
                return null;

            if (name.Length == 0)
                return name;

            try
            {

                // This is the primary method of the parser. It operates as a simple state machine storing information about types encountered as
                // they are encountered and back-propagating information to types as it is discovered.
                //
                // The BEST way to debug/understand this method is to simply step through it while watching genericArgs and nameSegments in the debugger watch window. The TypeNameSegment
                // has a DebuggerDisplayAttribute so you should see the state of the type as it goes through transitions (i.e. going from simple to generic with a known count of args, as
                // its unknown args are filled in seeing them appear in the debugger display string as we patch in the actual type names, etc...)
                ParsingState currentState = ParsingState.ParsingTypeName;

                // The name segment lists hold types as we are constructing them. These can be complete types (such as System.String) or partial types (like List<T> before we have parsed what T is)
                // or nested types (like the +Entry part of Dictionary<TKey, TEntry>+Entry) which may be later unified with the type they are nested in (we represent parses like Dictionary`2+Entry
                // as two TypeNameSegments (Dictionary`2 and Entry) and unify them later since when we extract Dictionary we don't yet know its generic arity or the fact it is the outer type in
                // a nested type).
                //
                // The nameSegments relate to top-level type names, the genericArgs relate to generic argument lists as we are parsing them. The genericArgs entries are back-propagated both to earlier
                // generic args as well as to types in the nameSegments list, as we complete the argument list parsing.
                List<TypeNameSegment>? nameSegments = null;
                List<TypeNameSegment>? genericArgs = null;

                // Local helper methods to help minimize code duplication
                void EnsureNameSegmentList()
                {
                    nameSegments ??= new List<TypeNameSegment>();
                }

                void EnsureGenericArgList()
                {
                    genericArgs ??= new List<TypeNameSegment>();
                }

                int curPos = 0;
                int parsingNestedClassDepth = 0;
                int parsingGenericArgListDepth = 0;

                while (ShouldContinueParsing(currentState))
                {
                    switch (currentState)
                    {
                        case ParsingState.ParsingTypeName:
                            {
                                // We are parsing a type name, this is the initial state as well as one entered into every time we are extracting a single type name through the course of parsing.
                                // This handles parsing nested types as well as generic param types.
                                //
                                // Parsing of type names is non-extractive, i.e. we don't substring the input string or create any new heap allocations to track them (apart from the two local
                                // Lists), we simply remember the extents of the name (start/end).

                                int start;
                                (start, curPos) = GetTypeNameExtent(name, curPos, parsingGenericArgList: parsingGenericArgListDepth != 0);

                                // Special case: Check if after parsing the type name we have exhausted the string, if so it means the input string is the output string, so just return it
                                // without allocating a copy.
                                if (ReturnOriginalDACString(curPos, name.Length, nameSegments))
                                    return name;

                                bool typeIsNestedClass = (parsingNestedClassDepth != 0);
                                if (parsingGenericArgListDepth == 0)
                                {
                                    // We are parsing a top-level type name/sequence
                                    EnsureNameSegmentList();

#pragma warning disable CS8602 // EnsureNameSegmentList call above ensures that nameSegments is never null here
                                    nameSegments.Add(new TypeNameSegment(name, (start, curPos), typeIsNestedClass, parsingArgDepth: 0));
#pragma warning restore CS8602
                                }
                                else
                                {
                                    // We are parsing a generic list (potentialy nested lists in the case where a generic param is itself generic)
                                    EnsureGenericArgList();

#pragma warning disable CS8602 // EnsureGenericArgList call above ensures that genericArgs is never null here
                                    genericArgs.Add(new TypeNameSegment(name, (start, curPos), typeIsNestedClass, parsingGenericArgListDepth));
#pragma warning restore CS8602

                                }

                                if (parsingNestedClassDepth != 0)
                                    parsingNestedClassDepth--;

                                (currentState, curPos) = DetermineNextStateAndPos(name, curPos);
                                break;
                            }
                        case ParsingState.ParsingNestedClass:
                            {
                                // We are starting to parse a nested type name, just record the nested class depth (we have to handle multiple levels of nested classes), and
                                // transition back to the type name parsing state.

                                parsingNestedClassDepth++;
                                currentState = ParsingState.ParsingTypeName;
                                break;
                            }
                        case ParsingState.ParsingGenericArgCount:
                            {
                                // Parse the arity of the generic type. Note: we do this 'in place' i.e. without extracting the count substring, it's unfortunate but int.Parse does not include
                                // an overload that operates in place based on start/length.

                                int genericArgCount;
                                (genericArgCount, curPos) = ParseGenericArityCountFromStringInPlace(name, curPos);

                                List<TypeNameSegment>? targetList = ((genericArgs != null) && (genericArgs.Count != 0)) ? genericArgs : nameSegments;

                                if (targetList != null)
                                {
                                    // NOTE: TypeNameSegment is a struct to avoid heap allocations, that means we have to extract / modify / re-store to ensure the updated state gets back into whatever
                                    // list this came from.
                                    int targetIndex = targetList.Count - 1;
                                    TypeNameSegment seg = targetList[targetIndex];
                                    seg.SetExpectedGenericArgCount(genericArgCount);
                                    targetList[targetIndex] = seg;
                                }
                                else
                                {
                                    currentState = ParsingState.Error;
                                    break;
                                }

                                (currentState, curPos) = DetermineNextStateAndPos(name, curPos);
                                break;
                            }
                        case ParsingState.ParsingGenericArgAssemblySpecifier:
                            {
                                // Nothing to do here, really, just skip the assembly name specified in the generic arg type
                                while (curPos < name.Length && name[curPos] != ']')
                                    curPos++;

                                (currentState, curPos) = DetermineNextStateAndPos(name, curPos);
                                break;
                            }
                        case ParsingState.ParsingGenericArgs:
                            {
                                // Start parsing the list of generic types, this just entails marking that we are parsing a generic arg list. NOTE: to support nested generic arg lsits we
                                // have to keep track of list count, not just a simple bool are/aren't parsing.
                                parsingGenericArgListDepth++;
                                currentState = ParsingState.ParsingTypeName;
                                break;
                            }
                        case ParsingState.ParsingArraySpecifier:
                            {
                                // Parse the array specifier, this mainly is to catch multi-dimensional arrays

                                // There is always at least a single dimenision in arrays, every comma counts as one more
                                int arrayDimensions = 1;

                                // Calculate the array dimensions
                                while ((curPos < name.Length) && (name[curPos] != ']'))
                                {
                                    if (name[curPos] == ',')
                                        arrayDimensions++;

                                    curPos++;
                                }

                                // Consume the final ] of the array specifier, unless we are at the end of the string already
                                if (curPos != name.Length)
                                    curPos++;

                                if (parsingGenericArgListDepth != 0 || nameSegments != null)
                                {
                                    // NOTE: TypeNameSegment is a struct to avoid heap allocations, that means we have to extract / modify / re-store to ensure the updated state gets back into whatever
                                    // list this came from.
                                    List<TypeNameSegment>? targetList = parsingGenericArgListDepth != 0 ? genericArgs : nameSegments;

                                    if (targetList != null)
                                    {
                                        int targetIndex = targetList.Count - 1;

                                        TypeNameSegment targetSegment = targetList[targetIndex];
                                        targetSegment.SetArrayDimensions(arrayDimensions);
                                        targetList[targetIndex] = targetSegment;
                                    }
                                    else
                                    {
                                        currentState = ParsingState.Error;
                                        break;
                                    }

                                    (currentState, curPos) = DetermineNextStateAndPos(name, curPos);
                                    if (genericArgs == null || genericArgs.Count == 0 && currentState == ParsingState.Done)
                                    {
                                        // Special case: Return original string in cases like this:
                                        //
                                        // System.String[,,,] or System.Int32[][]
                                        if (ReturnOriginalDACString(curPos, name.Length, nameSegments))
                                            return name;
                                    }
                                }
                                else
                                {
                                    Debug.Fail("Inside ParsingArraySpecifier but we don't think we are parsing generic params and have nothing on the top-level name segment list.");
                                    currentState = ParsingState.Error;
                                }

                                break;
                            }
                        case ParsingState.ResolveParsedGenericList:
                            {
                                // We are done with this level of arguments in terms of parsing, now we just have to apply them to the types they belong with (from previous parsing levels or the
                                // top-level).
                                parsingGenericArgListDepth--;

                                if (genericArgs == null || genericArgs.Count == 0)
                                {
                                    // For top-level types with multiple-level generic arg lists (so a type with a generic arg which itself is a generic type) as we unwind the nested generic args
                                    // lists we can end up wth no more work to do upon exiting a level (because we already propagated the info backwards before getting here), in which case, do nothing.
                                    (currentState, curPos) = DetermineNextStateAndPos(name, curPos);
                                    break;
                                }

                                (currentState, curPos) = ResolveParsedGenericList(name, curPos, parsingGenericArgListDepth, nameSegments, genericArgs);
                                break;
                            }
                    }
                }

                if (currentState == ParsingState.Error || nameSegments == null)
                {
                    // If we have encountered something we failed on, return the DAC string so at least there is SOMETHING
                    Debug.WriteLine($"Failed Parsing DAC Name: {name}");
                    return name;
                }

                //Build the final result from all the type name segments we have
                StringBuilder result = new();
                foreach (TypeNameSegment segment in nameSegments)
                    segment.ToString(result);

                return result.ToString();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Encountered an exception while parsing name {name}: {e}");
                return name;
            }
        }

        private static (ParsingState State, int CurrentPosition) ResolveParsedGenericList(string name, int currentPosition, int parsingGenericArgListDepth, List<TypeNameSegment>? nameSegments, List<TypeNameSegment>? genericArgs)
        {
            if (genericArgs == null)
            {
                return (ParsingState.Error, currentPosition);
            }

            // This is the most complicated part of the state machine, it involves back-propagating completed generic argument types into previous types they belong to.
            // It has to take care to propagate both amongst the genericArgs list as well as into the nameSegments list, it also has to ensure it unifies nested classes that
            // exist seperate from their parent type, before back-propagating that parent type.
            //
            // NOTE: This is called one time per generic list, so in the case of nested generics (where a param to a generic is itself another generic) this will be called
            // twice (and so on and so forth for arbitrary levels of nesting). What that means is that each call we only want to clear as many generics off our queue
            // as the generic types encountered on this parsing level require (whether that is in the generic arg list or the name segment list). And if we roll up generic
            // params into other entries in the generic param list we DON'T want to propagate anything to the name segment list since this generic arg list must be part of a nested
            // generic situation, not the top-level type name parsing

            int genericTargetIndex = -1;

            bool propagatedTypesToGenericArgs = false;

            // In some cases we end up with a genericArgs list where one entry is the fulfillment of another entry's generic args, this happens in cases like this
            //
            // System.Action`1[[System.Collections.Generic.IEnumerable`1[[Microsoft.VisualStudio.RemoteSettings.ActionResponse, Microsoft.VisualStudio.Telemetry]], mscorlib]]
            //
            // In this case System.Action is sitting on the nameSegment list waiting for its generic params, BUT our genericArgs stack has two entries, one for IEnumerable
            // (also waiting for its params) and one for ActionResponse, which is the arg to pair with the IEnumerable. So we have handle this arg rollup before we can
            // propagate the args to the nameSegments list
            //
            // NOTE: It is important to do this walk backwards since our list is being used like a queue and later entries bind with entries before them during genric arg
            // back-propagation
            //
            // NOTE: Purposely not using FindLastIndexOf because want to avoid allocation cost of lambda + indirection cost of callback during search
            genericTargetIndex = -1;
            for (int i = genericArgs.Count - 1; i >= 0; i--)
            {
                TypeNameSegment target = genericArgs[i];
                if (target.HasUnfulfilledGenericArgs && target.ParsingArgDepth == parsingGenericArgListDepth)
                {
                    genericTargetIndex = i;
                    break;
                }
            }

            while (genericTargetIndex != -1)
            {
                TypeNameSegment targetSegment = genericArgs[genericTargetIndex];

                propagatedTypesToGenericArgs = true;

                if (!TryPatchUnfulfilledGenericArgs(genericTargetIndex, genericArgs))
                    return (ParsingState.Error, currentPosition);

                int previousTarget = genericTargetIndex;
                genericTargetIndex = -1;

                for (int i = previousTarget - 1; i >= 0; i--)
                {
                    TypeNameSegment target = genericArgs[i];
                    if (target.HasUnfulfilledGenericArgs && target.ParsingArgDepth == parsingGenericArgListDepth)
                    {
                        genericTargetIndex = i;
                        break;
                    }
                }
            }

            // Roll up any nested classes at this level into their parents
            UnifyNestedClasses(parsingGenericArgListDepth, genericArgs);

            // If we haven't done any propagation amongst the generic args or we have but we have no more levels of generic args to parse, then we need to propagate
            // back into the top level type list, so find the appropriate type entry in that list and propagate args back to it to fulfill missing generics.
            if (!propagatedTypesToGenericArgs || (parsingGenericArgListDepth == 0))
            {
                if (nameSegments == null)
                {
                    Debug.Fail("Ended resolving generic arg list but no top-level types to propagate them to.");
                    return (ParsingState.Error, currentPosition);
                }

                // Fill the nameSegment generics with args, in order, from the genericArgs list. This works correctly whether the nameSegments list is a single generic or a
                // generic with a nested generic (so WeakKeyDictionary<T1,T2>+<WeakReference<T1>), unlike the special casing for such a situation we need to do while fixing up
                // the generic args list.
                int targetSegmentIndex = -1;
                for (int i = 0; i < nameSegments.Count; i++)
                {
                    if (nameSegments[i].HasUnfulfilledGenericArgs)
                    {
                        targetSegmentIndex = i;
                        break;
                    }
                }
                DebugOnly.Assert(targetSegmentIndex != -1, "Ended resolving generic arg list but failed to find any top-level types marked as having unfulfilled generic args to propagate them to.");

                if (targetSegmentIndex != -1)
                {
                    TypeNameSegment targetSegment = nameSegments[targetSegmentIndex];
                    while (genericArgs.Count != 0)
                    {
                        targetSegment.AddGenericArg(genericArgs[0]);
                        genericArgs.RemoveAt(0);

                        if (!targetSegment.HasUnfulfilledGenericArgs && (genericArgs.Count != 0))
                        {
                            // NOTE: TypeNameSegment is a struct to avoid heap allocations, that means we have to extract / modify / re-store to ensure the updated state gets back into whatever
                            // list this came from.
                            nameSegments[targetSegmentIndex] = targetSegment;

                            targetSegmentIndex = nameSegments.FindIndex(targetSegmentIndex, (tns) => tns.HasUnfulfilledGenericArgs);
                            if (targetSegmentIndex == -1)
                            {
                                return (ParsingState.Error, currentPosition);
                            }

                            targetSegment = nameSegments[targetSegmentIndex];
                        }
                    }

                    // NOTE: TypeNameSegment is a struct to avoid heap allocations, that means we have to extract / modify / re-store to ensure the updated state gets back into whatever
                    // list this came from.
                    nameSegments[targetSegmentIndex] = targetSegment;

                    DebugOnly.Assert(genericArgs.Count == 0, "Back-propagation to top-level generic types ended with generic args still in the genericArgs list.");

                    return DetermineNextStateAndPos(name, currentPosition);
                }
                else
                {
                    return (ParsingState.Error, currentPosition);
                }
            }
            else
            {
                return DetermineNextStateAndPos(name, currentPosition);
            }
        }

        private static bool TryPatchUnfulfilledGenericArgs(int genericTargetIndex, List<TypeNameSegment> genericArgs)
        {
            TypeNameSegment targetTypeToPatch = genericArgs[genericTargetIndex];
            DebugOnly.Assert(targetTypeToPatch.HasUnfulfilledGenericArgs, "Called TryPatchUnfulfilledGenericArgs with an index pointing at a TypeNameSegment that does not have unfulfilled generic params");

            // Patch ALL missing args from the target type, but no more. Any types before the target that need filling in will trigger another ResolveParsedGenericList state
            // to be entered as we unwind the parse, and at that point we will progate types back another level. This is very important to correctly parse horrific types like:
            //
            // Microsoft.CodeAnalysis.PooledObjects.ObjectPool<System.Collections.Generic.Stack<System.ValueTuple<Microsoft.CodeAnalysis.Shared.Collections.IntervalTree<Microsoft.CodeAnalysis.Editor.Shared.Tagging.TagSpanIntervalTree<Microsoft.VisualStudio.Text.Tagging.IBlockTag>+TagNode>+Node, System.Boolean>>>+Factory
            int nextTargetFulfillmentIndex = -1;
            while (targetTypeToPatch.HasUnfulfilledGenericArgs)
            {
                DebugOnly.Assert(genericTargetIndex + 1 != genericArgs.Count, "There are no arguments parsed following the target type for our generic arg back-propagation code. What do we patch with?");
                if (genericTargetIndex + 1 == genericArgs.Count)
                    return false;

                // NOTE: This is an annoyance of the DAC. Generally, when patching generic args into their owning type we can simply take the first arg after the generic
                // type entry itself and patch away. However, if we have a nested generic type whose parent is also a generic type then the type list for BOTH types are
                // given to us in a single flat list, in order from outer to inner.
                //
                // So for instance, an example of the simple case, is this:
                //
                // Foo<T1,T2>+Bar
                //
                // The DAC gives us this Foo`2+Bar[[[T1,assembly],[T2,assembly]]]
                //
                // In which case we simply patch the list into Foo front to back starting with the first genericArg entry AFTER the generic type are patching into
                //
                // However, for this:
                //
                // Foo<T1,T2>+Bar<U>
                //
                // the DAC will give us Foo`2+Bar`1[[[T1,assembly],[T2,assembly],[U,assembly]]]
                //
                // In this case the proper argument to patch into Bar is U (not T1). W simply need to skip the # of arguments in the list corresponding to the # of arguments
                // all parent types above us will consume from the list.
                if (targetTypeToPatch.IsNestedClass && targetTypeToPatch.IsGenericClass && (nextTargetFulfillmentIndex == -1))
                {
                    // The target to use to patch will be at least the arg after the type we are patching (genericTargetIndex + 1), but need to see how many nested and generic
                    // classes are above us at this same parse level so we can correctly pull arguments ala the rather large explanation above.
                    nextTargetFulfillmentIndex = genericTargetIndex + 1;
                    for (int i = genericTargetIndex - 1; i >= 0; i--)
                    {
                        // Don't flow back between levels of nested generic lists
                        if (genericArgs[i].ParsingArgDepth != targetTypeToPatch.ParsingArgDepth)
                            break;

                        if (genericArgs[i].IsGenericClass)
                            nextTargetFulfillmentIndex += genericArgs[i].RemainingUnfulfilledGenericArgCount;

                        // If the previous class itself is not nested, then we can stop searching, if it is, we have to continue
                        if (!genericArgs[i].IsNestedClass)
                            break;
                    }
                }

                // Look at every type after the target type that is missing generic parameter info. For any that are NOT nested classes (these are important to skip),
                // mark it as the next argument for argument back-propagation.
                //
                // NOTE: If nextTargetFulfillmentIndex is not -1 it means we can use it as the start position, it USED to point to the last argument we back-propagated, but
                // since we back-propagated it and removed it from the genericArgs list it now points at the next potential candidate for continued back-propagation.
                for (int i = (nextTargetFulfillmentIndex != -1 ? nextTargetFulfillmentIndex : genericTargetIndex + 1); i < genericArgs.Count; i++)
                {
                    if (!genericArgs[i].IsNestedClass)
                    {
                        nextTargetFulfillmentIndex = i;
                        break;
                    }
                }

                DebugOnly.Assert(nextTargetFulfillmentIndex != genericTargetIndex, "Ran out of args to back-propagate to satisfy generic requirements of earlier generic parameter.");
                if (nextTargetFulfillmentIndex == genericTargetIndex)
                    return false;

                // Add the located argument as a generic arg to our previous generic type
                targetTypeToPatch.AddGenericArg(genericArgs[nextTargetFulfillmentIndex]);

                // Remove the back-propagaeted argument from our argument list since it is now contained within targetTypeToPatch
                genericArgs.RemoveAt(nextTargetFulfillmentIndex);
            }

            // Patch our updated type info now that we have satisfied all of its generic arg requirements
            genericArgs[genericTargetIndex] = targetTypeToPatch;

            return true;
        }

        private static void UnifyNestedClasses(int parsingGenericArgListDepth, List<TypeNameSegment> genericArgs)
        {
            // Walk backwards so we can fold the multiple levels of nested classes backwards into the appropriate parent. It is important to do this BEFORE we propagate
            // anything into the nameSegment list, since propagation from here to that list means patching a generic arg in that type, as such we want to patch the full
            // (nested) type name, not the partial (outer parent) type name.
            for (int i = genericArgs.Count - 1; i >= 1; i--)
            {
                TypeNameSegment candidateSegment = genericArgs[i];

                // Once we hit an arg that came from a parsing arg depth < than the current, we can stop, we won't be processing any of those args yet, we will when we unwind
                // that parsing level
                if (candidateSegment.ParsingArgDepth < parsingGenericArgListDepth)
                    break;

                // Don't fold any that still have unfulfilled generics, leave them so they can be fulfilled
                //
                // TODO: Is this a thing? The need to check for HasUnfulfilledGenericArgs? The code before this that propagates generic args only fixes a single generic type target
                // per pass...can there ever be more than one? The closing of a generic param list triggers this code, so the only way for there to be more than one generic type needing
                // patching is EITHER a nested generic type in a generic parent or a generic param to a generic type, but the latter will trigger two seperate levels of generic arg parsing,
                // the former is handled in the back-propagation code before this, and this collapses the nested generic (that has been resolved) into the parent generic (that may not
                // yet have been resolved).
                if (candidateSegment.IsNestedClass && !candidateSegment.HasUnfulfilledGenericArgs)
                {
                    TypeNameSegment toMergeWith = genericArgs[i - 1];
                    toMergeWith.UnifyNestedClass(candidateSegment);
                    genericArgs.RemoveAt(i);

                    genericArgs[i - 1] = toMergeWith;
                }
            }
        }

        private static bool ShouldContinueParsing(ParsingState state)
        {
            return ((state != ParsingState.Done) && (state != ParsingState.Error));
        }

        // Special cases:
        //
        // Simple name: Microsoft.TeamFoundation.Git.Contracts.GitBranch
        // Simple name + simple nested name: MS.Internal.XamlNamespaces+<get_All>d__5
        // Arrays of either of the previous two
        //
        // In all of these cases the DAC string IS the string we want to use, so we don't want to do any more allocation, just return the DAC string, this is optimal.
        private static bool ReturnOriginalDACString(int curPos, int inputStringLength, List<TypeNameSegment>? queuedNameSegments)
        {
            if (curPos != inputStringLength)
                return false;

            // Ex: System.String
            if (queuedNameSegments == null)
                return true;

            // Ex: Microsoft.VisualStudio.Imaging.SourceDescriptor+<>c__DisplayClass50_0+<<LoadBitmappedImage>b__0>d
            // or
            //
            // System.Collections.Immutable.ImmutableDictionary`2
            //
            // NOTE: The second type is generic but the DAC has given us no generic paramts, so we can't do anything more with the string
            return queuedNameSegments.TrueForAll((tns) => !tns.IsGenericClass || (tns.ExpectedGenericArgsCount == tns.RemainingUnfulfilledGenericArgCount));
        }

        private static (int Start, int End) GetTypeNameExtent(string name, int cur, bool parsingGenericArgList)
        {
            int start = cur;

            // Have to be aware the ` character is not ALWAYS a generic arity specifier, because of 'names' like this
            //
            // CAdviseEvents<IVsSolutionBuildManager\,IVsUpdateSolutionEvents\,{IVsSolutionBuildManager::`vcall'{20}'\,0}\,{IVsSolutionBuildManager::`vcall'{24}'\,0}>
            //
            while (cur < name.Length &&
                   name[cur] != '[' &&
                   (name[cur] != ',' || !parsingGenericArgList) &&
                   (name[cur] != '+' || parsingGenericArgList) && // allow + when parsing generic arg type names because generic arg names can be nested types
                   (name[cur] != '`' || ((cur + 1 < name.Length) && !char.IsDigit(name[cur + 1]))))
            {
                cur++;
            }

            return (start, cur);
        }

        private static (int Count, int Pos) ParseGenericArityCountFromStringInPlace(string input, int curPos)
        {
            DebugOnly.Assert(char.IsDigit(input[curPos]));

            int digitSpanStart = curPos;
            while (curPos < input.Length && char.IsDigit(input[curPos]))
                curPos++;

            int value = 0;
            int multAmt = 1;

            for (int i = curPos - 1; i >= digitSpanStart; i--)
            {
                value += ((input[i] - '0') * multAmt);
                multAmt *= 10;
            }

            return (value, curPos);
        }

        private static (ParsingState State, int Pos) DetermineNextStateAndPos(string name, int curPos)
        {
            if (curPos == name.Length)
                return (ParsingState.Done, curPos);

            switch (name[curPos])
            {
                case ArgSeperator:
                    {
                        return (ParsingState.ParsingGenericArgAssemblySpecifier, curPos + 1);
                    }
                case GenericAritySpecifier:
                    {
                        return (ParsingState.ParsingGenericArgCount, curPos + 1);
                    }
                case NestedClassSpecifier:
                    {
                        return (ParsingState.ParsingNestedClass, curPos + 1);
                    }
                case GenericArgListOrArrayStartSpecifier:
                    {
                        if (curPos + 1 != name.Length)
                        {
                            if (name[curPos + 1] == GenericArgListOrArrayEndSpecifier || name[curPos + 1] == ArgSeperator)
                                return (ParsingState.ParsingArraySpecifier, curPos);
                            else if (name[curPos + 1] == GenericArgListOrArrayStartSpecifier)
                                return (ParsingState.ParsingGenericArgs, curPos + 2);
                        }

                        return (ParsingState.Error, curPos);
                    }
                case GenericArgListOrArrayEndSpecifier:
                    {
                        if (curPos != name.Length)
                        {
                            if (name[curPos + 1] == GenericArgListOrArrayEndSpecifier)
                                return (ParsingState.ResolveParsedGenericList, curPos + 2);
                            else if (name[curPos + 1] == ArgSeperator)
                            {
                                // +3 because cur pos == ']' and next position is a ',', which means we must have a list like this:
                                //
                                // [[Microsoft.VisualStudio.Threading.IJoinableTaskDependent, Microsoft.VisualStudio.Threading],[System.Int32, mscorlib],[Microsoft.VisualStudio.Threading.IJoinableTaskDependent, Microsoft.VisualStudio.Threading]]
                                //
                                // and thus curPos + 3 == '[', which we want to skip as well.
                                return (ParsingState.ParsingTypeName, curPos + 3);
                            }
                        }

                        return (ParsingState.Error, curPos);
                    }
                default:
                    return (ParsingState.Error, curPos);
            }
        }
        private enum ParsingState
        {
            ParsingTypeName,
            ParsingArraySpecifier,
            ParsingNestedClass,
            ParsingGenericArgCount,
            ParsingGenericArgs,
            ParsingGenericArgAssemblySpecifier,
            ResolveParsedGenericList,
            Error,
            Done
        }

        [DebuggerDisplay("{Name}")]
        private struct TypeNameSegment
        {
            private readonly string _input;
            private int _expectedGenericArgCount;
            private TypeNameSegment[]? _typeArgSegments;
            private int _nextUnfulfilledGenericArgSlot;
            private int _arrayDimensions;
            private int _arrayOfArraysCount;

            // NOTE: This is only an array to defeat the cycle detection stuff in the compiler. Since this is a struct if I have a field that is TypeNameSegment or
            // TypeNameSegment? it complains there is a cycle in the layout since to determine the size of the struct it must determine the size of the struct. To
            // avoid that we store a nested class (and there will only ever be at most 1) in an array.
            private TypeNameSegment[]? _nestedClass;

            public TypeNameSegment(string input, (int Start, int End) extent, bool isNestedClass, int parsingArgDepth)
            {
                _input = input;
                Extent = extent;
                IsNestedClass = isNestedClass;
                _expectedGenericArgCount = 0;
                _typeArgSegments = null;
                _nextUnfulfilledGenericArgSlot = 0;
                _arrayDimensions = 0;
                _arrayOfArraysCount = 0;
                ParsingArgDepth = parsingArgDepth;
                _nestedClass = null;
            }

            public (int Start, int End) Extent { get; }

            public string Name => ToString();

            public int ParsingArgDepth { get; }

            public bool IsNestedClass { get; }

            public bool IsNestedClassOrHasNestedClassComponent => IsNestedClass || HasNestedClassSegment;

            public bool IsGenericClass => _expectedGenericArgCount != 0;

            public bool HasUnfulfilledGenericArgs => IsGenericClass && (_typeArgSegments == null || _nextUnfulfilledGenericArgSlot < _typeArgSegments.Length);

            public int ExpectedGenericArgsCount => _expectedGenericArgCount;

            public int RemainingUnfulfilledGenericArgCount => _expectedGenericArgCount - _nextUnfulfilledGenericArgSlot;

            public void SetArrayDimensions(int dimensions)
            {
                // There are two distinct versions of multi-dimensional arrays we represent in a single TypeNameSegment. There
                // is the classic multidimensional array (ala int[,]) and then there is the array of arrays specifier (int[][]).
                // Instead of creating a seperate TypeNameSegment for the latter that is just indicating 'an array of the previous
                // name segment', we just pile them all into the original TypeNameSegment and differentiate when we are printing
                // out the names at the end (to either print [,] or [][]).

                if (_arrayDimensions == 0)
                    _arrayDimensions = dimensions;
                else
                    _arrayOfArraysCount++;
            }

            public void SetExpectedGenericArgCount(int expectedCount)
            {
                _expectedGenericArgCount = expectedCount;
            }

            public void AddGenericArg(TypeNameSegment arg)
            {
                DebugOnly.Assert(_expectedGenericArgCount != 0, $"{Name} did not expect any generic arguments");

                if (_expectedGenericArgCount == 0)
                    return;

                _typeArgSegments ??= new TypeNameSegment[_expectedGenericArgCount];
                _typeArgSegments[_nextUnfulfilledGenericArgSlot++] = arg;
            }

            public void UnifyNestedClass(TypeNameSegment nestedClass)
            {
                DebugOnly.Assert(nestedClass.IsNestedClass);
                _nestedClass = new[] { nestedClass };
            }

            public override string ToString()
            {
                StringBuilder result = new();

                ToString(result);

                return result.ToString();
            }

            public void ToString(StringBuilder destination)
            {
                if (IsNestedClass)
                    destination.Append('+');

                destination.Append(_input, Extent.Start, Extent.End - Extent.Start);

                if (IsGenericClass)
                {
                    OutputTypeArguments(destination, _nextUnfulfilledGenericArgSlot, _expectedGenericArgCount, _typeArgSegments);
                }

                _nestedClass?[0].ToString(destination);

                // See comment in SetArrayDimensions on what this is :)
                if (_arrayOfArraysCount != 0)
                {
                    destination.Append("[]");
                    for (int i = 0; i < _arrayOfArraysCount; i++)
                        destination.Append("[]");
                }
                else if (_arrayDimensions != 0)
                {
                    destination.Append('[');
                    if (_arrayDimensions > 1)
                        destination.Append(',', _arrayDimensions - 1);
                    destination.Append(']');
                }
            }

            private bool HasNestedClassSegment => _nestedClass != null;

            private static void OutputTypeArguments(StringBuilder destination, int firstMissingGenericSlot, int expectedArgCount, TypeNameSegment[]? typeArgs)
            {
                if (typeArgs == null)
                {
                    destination.Append('<');
                    AddMissingArgumentInfo(destination, firstMissingGenericSlot, expectedArgCount);
                    destination.Append('>');
                }
                else
                {
                    destination.Append('<');
                    for (int i = 0; i < firstMissingGenericSlot; i++)
                    {
                        if (i != 0)
                            destination.Append(", ");

                        typeArgs[i].ToString(destination);
                    }

                    if (firstMissingGenericSlot < expectedArgCount)
                        AddMissingArgumentInfo(destination, firstMissingGenericSlot, expectedArgCount);

                    destination.Append('>');
                }
            }

            private static void AddMissingArgumentInfo(StringBuilder result, int firstMissingGenericSlot, int expectedArgCount)
            {
                // This is an error case where the DAC gave no generic args or incomplete generic args for a type
                for (int i = firstMissingGenericSlot; i < expectedArgCount; i++)
                {
                    if (i != 0)
                        result.Append(", ");

                    result.Append("T" + (i + 1).ToString(CultureInfo.CurrentCulture));
                }
            }
        }
    }
}