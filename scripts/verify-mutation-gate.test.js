// Copyright © Erickson Lopez. MIT License.
const assert = require('assert');
const {
  loadThresholds,
  parseScoreFromDescription,
  evaluateScore,
  verifyMutationGate,
  MAX_REPORT_AGE_DAYS
} = require('./verify-mutation-gate');

console.log('Running tests for verify-mutation-gate.js...\n');

// Test 1: loadThresholds from stryker-config.json
{
  const thresholds = loadThresholds();
  assert.strictEqual(thresholds.high, 100, 'Threshold high should be 100');
  assert.strictEqual(thresholds.low, 98, 'Threshold low should be 98');
  assert.strictEqual(thresholds.break, 95, 'Threshold break should be 95');
  console.log('✅ Test 1 Passed: loadThresholds loads correct values from stryker-config.json');
}

// Test 2: parseScoreFromDescription
{
  assert.strictEqual(parseScoreFromDescription('Score: 100% (11/11 packages >= 95%) - ✅ HIGH'), 100);
  assert.strictEqual(parseScoreFromDescription('Score: 98.5% (11/11 packages >= 95%) - 🟡 LOW'), 98.5);
  assert.strictEqual(parseScoreFromDescription('Score: 95.0% - 🟠 WARNING'), 95.0);
  assert.strictEqual(parseScoreFromDescription('Score: 94.2% - ❌ FAILED'), 94.2);
  assert.strictEqual(parseScoreFromDescription(null), null);
  assert.strictEqual(parseScoreFromDescription('No percentage here'), null);
  console.log('✅ Test 2 Passed: parseScoreFromDescription correctly extracts numeric percentage');
}

// Test 3: evaluateScore
{
  const thresholds = { high: 100, low: 98, break: 95 };

  const resHigh = evaluateScore(100, thresholds);
  assert.strictEqual(resHigh.status, '✅ HIGH');
  assert.strictEqual(resHigh.passedBreak, true);

  const resLow = evaluateScore(98.5, thresholds);
  assert.strictEqual(resLow.status, '🟡 LOW');
  assert.strictEqual(resLow.passedBreak, true);

  const resWarn = evaluateScore(96.0, thresholds);
  assert.strictEqual(resWarn.status, '🟠 WARNING');
  assert.strictEqual(resWarn.passedBreak, true);

  const resBreakExact = evaluateScore(95.0, thresholds);
  assert.strictEqual(resBreakExact.status, '🟠 WARNING');
  assert.strictEqual(resBreakExact.passedBreak, true);

  const resFail = evaluateScore(94.9, thresholds);
  assert.strictEqual(resFail.status, '❌ FAILED');
  assert.strictEqual(resFail.passedBreak, false);

  console.log('✅ Test 3 Passed: evaluateScore correctly categorizes scores and break gate');
}

// Test 4: verifyMutationGate with mock direct target SHA
(async () => {
  let failed = false;
  const mockContext = {
    repo: { owner: 'ericksonlopezf', repo: 'dotnet-transaction' },
    sha: 'abc1234567890'
  };

  const freshDate = new Date().toISOString();

  const mockGithub = {
    rest: {
      repos: {
        listCommits: async () => ({
          data: [{ sha: 'abc1234567890' }]
        }),
        getCombinedStatusForRef: async ({ ref }) => {
          if (ref === 'abc1234567890') {
            return {
              data: {
                statuses: [
                  {
                    context: 'mutation-testing/stryker',
                    state: 'success',
                    description: 'Score: 100% (11/11 packages >= 95%) - ✅ HIGH',
                    updated_at: freshDate,
                    target_url: 'https://github.com/ericksonlopezf/dotnet-transaction/actions/runs/12345'
                  }
                ]
              }
            };
          }
          return { data: { statuses: [] } };
        }
      }
    }
  };

  const mockCore = {
    setOutput: () => {},
    summary: { addRaw: () => ({ write: () => {} }) }
  };

  const res = await verifyMutationGate({ github: mockGithub, context: mockContext, core: mockCore });
  assert.strictEqual(res.needsStryker, false, 'Should not need stryker when valid commit status exists');
  assert.strictEqual(res.canProceed, true, 'Should allow release when 100% score');
  console.log('✅ Test 4 Passed: verifyMutationGate succeeds with direct 100% commit status');
})();

// Test 5: verifyMutationGate with score below break threshold
(async () => {
  const mockContext = {
    repo: { owner: 'ericksonlopezf', repo: 'dotnet-transaction' },
    sha: 'fail1234567890'
  };

  const freshDate = new Date().toISOString();

  const mockGithub = {
    rest: {
      repos: {
        listCommits: async () => ({
          data: [{ sha: 'fail1234567890' }]
        }),
        getCombinedStatusForRef: async () => {
          return {
            data: {
              statuses: [
                {
                  context: 'mutation-testing/stryker',
                  state: 'failure',
                  description: 'Score: 80.0% - ❌ FAILED',
                  updated_at: freshDate,
                  target_url: 'https://github.com/ericksonlopezf/dotnet-transaction/actions/runs/12346'
                }
              ]
            }
          };
        }
      }
    }
  };

  const mockCore = {
    setOutput: () => {},
    summary: { addRaw: () => ({ write: () => {} }) }
  };

  const res = await verifyMutationGate({ github: mockGithub, context: mockContext, core: mockCore });
  assert.strictEqual(res.needsStryker, true, 'needsStryker should be true for sub-break score');
  assert.strictEqual(res.canProceed, false, 'canProceed should be false for sub-break score');
  console.log('✅ Test 5 Passed: verifyMutationGate triggers conditional stryker run for sub-break score');
})();

// Test 6: verifyMutationGate with score 95.0% (WARNING threshold - release allowed)
(async () => {
  const mockContext = {
    repo: { owner: 'ericksonlopezf', repo: 'dotnet-transaction' },
    sha: 'warn1234567890'
  };

  const freshDate = new Date().toISOString();

  const mockGithub = {
    rest: {
      repos: {
        listCommits: async () => ({
          data: [{ sha: 'warn1234567890' }]
        }),
        getCombinedStatusForRef: async ({ ref }) => {
          if (ref === 'warn1234567890') {
            return {
              data: {
                statuses: [
                  {
                    context: 'mutation-testing/stryker',
                    state: 'success',
                    description: 'Score: 95.0% - 🟠 WARNING',
                    updated_at: freshDate,
                    target_url: 'https://github.com/ericksonlopezf/dotnet-transaction/actions/runs/12347'
                  }
                ]
              }
            };
          }
          return { data: { statuses: [] } };
        }
      }
    }
  };

  const mockCore = {
    setOutput: () => {},
    summary: { addRaw: () => ({ write: () => {} }) }
  };

  const res = await verifyMutationGate({ github: mockGithub, context: mockContext, core: mockCore });
  assert.strictEqual(res.needsStryker, false, 'needsStryker should be false for 95.0% score');
  assert.strictEqual(res.canProceed, true, 'canProceed should be true for 95.0% score');
  console.log('✅ Test 6 Passed: verifyMutationGate allows release for 95.0% WARNING score');
})();

// Test 7: verifyMutationGate with score 98.0% (LOW threshold - release allowed)
(async () => {
  const mockContext = {
    repo: { owner: 'ericksonlopezf', repo: 'dotnet-transaction' },
    sha: 'low1234567890'
  };

  const freshDate = new Date().toISOString();

  const mockGithub = {
    rest: {
      repos: {
        listCommits: async () => ({
          data: [{ sha: 'low1234567890' }]
        }),
        getCombinedStatusForRef: async ({ ref }) => {
          if (ref === 'low1234567890') {
            return {
              data: {
                statuses: [
                  {
                    context: 'mutation-testing/stryker',
                    state: 'success',
                    description: 'Score: 98.0% - 🟡 LOW',
                    updated_at: freshDate,
                    target_url: 'https://github.com/ericksonlopezf/dotnet-transaction/actions/runs/12348'
                  }
                ]
              }
            };
          }
          return { data: { statuses: [] } };
        }
      }
    }
  };

  const mockCore = {
    setOutput: () => {},
    summary: { addRaw: () => ({ write: () => {} }) }
  };

  const res = await verifyMutationGate({ github: mockGithub, context: mockContext, core: mockCore });
  assert.strictEqual(res.needsStryker, false, 'needsStryker should be false for 98.0% score');
  assert.strictEqual(res.canProceed, true, 'canProceed should be true for 98.0% score');
  console.log('✅ Test 7 Passed: verifyMutationGate allows release for 98.0% LOW score');
})();

// Test 8: verifyMutationGate with expired report (> 7 days)
(async () => {
  const mockContext = {
    repo: { owner: 'ericksonlopezf', repo: 'dotnet-transaction' },
    sha: 'exp1234567890'
  };

  // 10 days ago
  const oldDate = new Date(Date.now() - 10 * 24 * 60 * 60 * 1000).toISOString();

  const mockGithub = {
    rest: {
      repos: {
        listCommits: async () => ({
          data: [{ sha: 'exp1234567890' }]
        }),
        getCombinedStatusForRef: async () => {
          return {
            data: {
              statuses: [
                {
                  context: 'mutation-testing/stryker',
                  state: 'success',
                  description: 'Score: 100% - ✅ HIGH',
                  updated_at: oldDate,
                  target_url: 'https://github.com/ericksonlopezf/dotnet-transaction/actions/runs/12349'
                }
              ]
            }
          };
        }
      }
    }
  };

  const mockCore = {
    setOutput: () => {},
    summary: { addRaw: () => ({ write: () => {} }) }
  };

  const res = await verifyMutationGate({ github: mockGithub, context: mockContext, core: mockCore });
  assert.strictEqual(res.needsStryker, true, 'needsStryker should be true for expired report (> 7 days)');
  console.log('✅ Test 8 Passed: verifyMutationGate triggers conditional run for expired report (> 7 days)');
})();

// Test 9: verifyMutationGate with code drift in src/
(async () => {
  const mockContext = {
    repo: { owner: 'ericksonlopezf', repo: 'dotnet-transaction' },
    sha: 'newSha123456789'
  };

  const freshDate = new Date().toISOString();

  const mockGithub = {
    rest: {
      repos: {
        listCommits: async () => {
          return {
            data: [
              { sha: 'newSha123456789' },
              { sha: 'oldEvaluatedCommit123' }
            ]
          };
        },
        getCombinedStatusForRef: async ({ ref }) => {
          if (ref === 'oldEvaluatedCommit123') {
            return {
              data: {
                statuses: [
                  {
                    context: 'mutation-testing/stryker',
                    state: 'success',
                    description: 'Score: 100% - ✅ HIGH',
                    updated_at: freshDate,
                    target_url: 'https://github.com/ericksonlopezf/dotnet-transaction/actions/runs/12350'
                  }
                ]
              }
            };
          }
          return { data: { statuses: [] } };
        },
        compareCommits: async () => {
          return {
            data: {
              files: [
                { filename: 'src/EricksonLopez.Transaction/TransactionManager.cs' },
                { filename: 'README.md' }
              ]
            }
          };
        }
      }
    }
  };

  const mockCore = {
    setOutput: () => {},
    summary: { addRaw: () => ({ write: () => {} }) }
  };

  const res = await verifyMutationGate({ github: mockGithub, context: mockContext, core: mockCore });
  assert.strictEqual(res.needsStryker, true, 'needsStryker should be true when src/ code was modified');
  console.log('✅ Test 9 Passed: verifyMutationGate triggers conditional run when src/ code was modified');
})();
