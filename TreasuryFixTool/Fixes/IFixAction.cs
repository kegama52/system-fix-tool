using System.Threading.Tasks;

namespace TreasuryFixTool.Fixes
{
    /// <summary>
    /// Defines the contract for a fix action.
    /// </summary>
    public interface IFixAction
    {
        /// <summary>
        /// Gets the name of the fix action.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the description of the fix action.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Executes the fix action asynchronously.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains the outcome of the fix.</returns>
        Task<FixResult> ExecuteAsync();
    }

    /// <summary>
    /// Represents the result of a fix action.
    /// </summary>
    public class FixResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
    }
}