using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions.Common;
using FluentAssertions.Execution;

namespace FluentAssertions.Equivalency
{
    /// <summary>
    /// Is responsible for validating the equality of one or more properties of a subject with another object.
    /// </summary>
    public class EquivalencyValidator : IEquivalencyValidator
    {
        private const string CyclicReferenceDetectorKey = "cyclic_reference_detector";

        #region Private Definitions

        private readonly IEquivalencyAssertionOptions config;

        private readonly Dictionary<Type, bool> isComplexTypeMap = new Dictionary<Type, bool>();

        #endregion

        public EquivalencyValidator(IEquivalencyAssertionOptions config)
        {
            this.config = config;
        }

        public void AssertEquality(EquivalencyValidationContext context)
        {
            using (var scope = new AssertionScope())
            {
                scope.AddReportable("configuration", config.ToString());

                scope.BecauseOf(context.Because, context.BecauseArgs);

                AssertEqualityUsing(context);

                if (context.Tracer != null)
                {
                    scope.AddReportable("trace", context.Tracer.ToString());
                }
            }
        }

        public void AssertEqualityUsing(IEquivalencyValidationContext context)
        {
            var depth = context.SelectedMemberPath.Count(chr => chr == '.');
            var shouldRecurse = config.AllowInfiniteRecursion || depth < 10;

            if (!shouldRecurse)
            {
                AssertionScope.Current.FailWith("The maximum recursion depth was reached.  ");
                return;
            }

            if (context.SelectedMemberDescription.Length > 0)
            {
                AssertionScope.Current.Context = context.SelectedMemberDescription;
            }

            AssertionScope.Current.TrackComparands(context.Subject, context.Expectation);

            var objectTracker = AssertionScope.Current.Get<CyclicReferenceDetector>(CyclicReferenceDetectorKey);
            if (objectTracker is null)
            {
                objectTracker = new CyclicReferenceDetector(config.CyclicReferenceHandling);
                AssertionScope.Current.AddNonReportable(CyclicReferenceDetectorKey, objectTracker);
            }

            var isComplexType = false;
            var type = context.Expectation?.GetType();

            if (type != null && !isComplexTypeMap.TryGetValue(type, out isComplexType))
            {
                isComplexType = !type.OverridesEquals();
                isComplexTypeMap[type] = isComplexType;
            }

            var reference = new ObjectReference(context.Expectation, context.SelectedMemberPath, isComplexType);
            if (objectTracker.IsCyclicReference(reference, context.Because, context.BecauseArgs))
            {
                return;
            }

            var wasHandled = AssertionOptions.EquivalencySteps
                .Where(step => step.CanHandle(context, config))
                .Any(step => step.Handle(context, this, config));

            if (!wasHandled)
            {
                Execute.Assertion.FailWith("No IEquivalencyStep was found to handle the context. ");
            }
        }
    }
}
