using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TreasuryFixTool.Fixes;

namespace TreasuryFixTool.Fixes
{
    /// <summary>
    /// Orchestrates executing fix actions.
    /// </>
    public class FixEngine
    {
        private readonly List<IFixAction> _fixActions;
        private static readonly Dictionary<string, string> CheckToFixMap = new()
        {
            { "Disk Space Check", "Disk Cleanup" },
            { "Network Check", "Network Reset" },
            { "Memory Check", "Windows Update" },
            { "Service Check", "Service Restart" }
        };

        public FixEngine()
        {
            _fixActions = new List<IFixAction>
            {
                new DiskCleanupFix(),
                new ServiceRestartFix(),
                new NetworkResetFix(),
                new TempFilesFix(),
                new SfcDismFix(),
                new WindowsUpdateFix()
            };
        }

        public string? GetFixNameForCheck(string checkName) => CheckToFixMap.GetValueOrDefault(checkName);

        /// <summary>
        /// Executes all fix actions sequentially.
        /// </summary>
        /// <returns>A list of fix results.</returns>
        public async Task<List<FixResult>> ExecuteAllFixesAsync()
        {
            var results = new List<FixResult>();
            foreach (var fix in _fixActions)
            {
                try
                {
                    var result = await fix.ExecuteAsync();
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    results.Add(new FixResult
                    {
                        Success = false,
                        Message = $"Exception during fix {fix.Name}: {ex.Message}",
                        Details = ex.ToString()
                    });
                }
            }
            return results;
        }

        /// <summary>
        /// Executes a specific fix action by name.
        /// </summary>
        /// <param name="fixName">The name of the fix to execute.</param>
        /// <returns>The fix result.</returns>
        public async Task<FixResult> ExecuteFixAsync(string fixName)
        {
            var fix = _fixActions.Find(f => f.Name.Equals(fixName, StringComparison.OrdinalIgnoreCase));
            if (fix == null)
            {
                return new FixResult
                {
                    Success = false,
                    Message = $"Fix action '{fixName}' not found."
                };
            }

            try
            {
                return await fix.ExecuteAsync();
            }
            catch (Exception ex)
            {
                return new FixResult
                {
                    Success = false,
                    Message = $"Exception during fix {fix.Name}: {ex.Message}",
                    Details = ex.ToString()
                };
            }
        }
    }
}