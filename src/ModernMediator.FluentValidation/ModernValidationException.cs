using System;
using System.Collections.Generic;
using FluentValidation.Results;

namespace ModernMediator.FluentValidation
{
    /// <summary>
    /// Exception thrown when one or more FluentValidation validators report failures
    /// during the ModernMediator pipeline.
    /// </summary>
    public class ModernValidationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ModernValidationException"/> with the specified validation failures.
        /// </summary>
        /// <param name="errors">The validation failures that caused this exception.</param>
        public ModernValidationException(IReadOnlyList<ValidationFailure> errors)
            : base($"Validation failed with {errors.Count} error(s).")
        {
            Errors = errors;
        }

        /// <summary>
        /// The validation failures that caused this exception.
        /// </summary>
        public IReadOnlyList<ValidationFailure> Errors { get; }
    }
}
