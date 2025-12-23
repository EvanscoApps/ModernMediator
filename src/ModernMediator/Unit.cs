using System;

namespace ModernMediator
{
    /// <summary>
    /// Represents a void type, since void is not a valid return type in C#.
    /// Use this as the response type for requests that don't return a meaningful value.
    /// </summary>
    public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
    {
        /// <summary>
        /// The single value of the Unit type.
        /// </summary>
        public static readonly Unit Value = new();

        /// <summary>
        /// Returns a completed task with Unit value.
        /// Useful for async handlers that don't return a value.
        /// </summary>
        public static readonly Task<Unit> Task = System.Threading.Tasks.Task.FromResult(Value);

        /// <inheritdoc />
        public bool Equals(Unit other) => true;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Unit;

        /// <inheritdoc />
        public override int GetHashCode() => 0;

        /// <inheritdoc />
        public int CompareTo(Unit other) => 0;

        /// <inheritdoc />
        public int CompareTo(object? obj) => 0;

        /// <inheritdoc />
        public override string ToString() => "()";

        /// <summary>
        /// Equality operator.
        /// </summary>
        public static bool operator ==(Unit left, Unit right) => true;

        /// <summary>
        /// Inequality operator.
        /// </summary>
        public static bool operator !=(Unit left, Unit right) => false;
    }
}