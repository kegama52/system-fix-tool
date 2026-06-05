const { scanLogs, clearCache } = require('../src/diagnostics');
const { analyzeLogWithAI } = require('../src/ai-log-analyzer');

describe('System Diagnostic Utilities', () => {
  test('scanLogs should identify corrupted file patterns', () => {
    const mockLogs = 'Error: File config.sys is corrupted';
    const result = scanLogs(mockLogs);

    expect(result.matched).toContain('config.sys');
    expect(result.isCorrupted).toBe(true);
    expect(result.summary).toMatch(/Potential corruption detected/);
  });

  test('clearCache should return success status', () => {
    const result = clearCache();
    expect(result.status).toBe('success');
    expect(result.message).toMatch(/Cache cleared successfully/);
  });
});

describe('AI Log Analyzer', () => {
  test('analyzeLogWithAI should simulate prompt engineering output', async () => {
    const systemLog = 'Disk error detected during update.';
    const analysis = await analyzeLogWithAI(systemLog);

    expect(analysis.prompt_used).toContain(systemLog);
    expect(analysis.ai_diagnosis).toBe('Corrupted update cache detected.');
    expect(analysis.suggested_action).toBe('Run clearCache() utility.');
  });
});
