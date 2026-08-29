// Copyright © Erickson Lopez. MIT License.
const fs = require('fs');
const path = require('path');
const https = require('https');

function loadThresholds(configPath = 'stryker-config.json') {
  let thresholds = { high: 100, low: 98, break: 95 };
  try {
    if (fs.existsSync(configPath)) {
      const config = JSON.parse(fs.readFileSync(configPath, 'utf8'));
      const t = config['stryker-config']?.thresholds || config.thresholds || {};
      thresholds = {
        high: Number(t.high ?? 100),
        low: Number(t.low ?? 98),
        break: Number(t.break ?? 95)
      };
    }
  } catch (err) {
    console.warn(`Could not parse ${configPath}: ${err.message}`);
  }
  return thresholds;
}

function findJsonReports(dir) {
  let results = [];
  if (!fs.existsSync(dir)) return results;
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      results = results.concat(findJsonReports(full));
    } else if (
      entry.name.endsWith('.json') &&
      !entry.name.endsWith('.html.json') &&
      !entry.name.endsWith('metadata.json') &&
      !entry.name.startsWith('summary-')
    ) {
      results.push(full);
    }
  }
  return results;
}

function getStatusLabel(score, thresholds, total) {
  if (total === 0) return '✅ HIGH';
  if (score >= thresholds.high) return '✅ HIGH';
  if (score >= thresholds.low) return '🟡 LOW';
  if (score >= thresholds.break) return '🟠 WARNING';
  return '❌ FAILED';
}

function postCommitStatus(owner, repo, sha, token, statusData) {
  return new Promise((resolve) => {
    if (!token || !owner || !repo || !sha || sha === 'unknown') {
      console.log('Skipping GitHub commit status creation (missing token/repo/sha).');
      return resolve(false);
    }

    const payload = JSON.stringify({
      state: statusData.state, // 'success' | 'failure'
      target_url: statusData.target_url || undefined,
      description: statusData.description || 'Stryker Mutation Quality Gate',
      context: 'quality-gate/stryker-mutation'
    });

    const options = {
      hostname: 'api.github.com',
      port: 443,
      path: `/repos/${owner}/${repo}/statuses/${sha}`,
      method: 'POST',
      headers: {
        'User-Agent': 'dotnet-stryker-quality-gate',
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
        'Content-Length': Buffer.byteLength(payload)
      }
    };

    const req = https.request(options, (res) => {
      let body = '';
      res.on('data', (d) => { body += d; });
      res.on('end', () => {
        if (res.statusCode >= 200 && res.statusCode < 300) {
          console.log(`✅ Successfully published commit status 'quality-gate/stryker-mutation' (${statusData.state}) to commit ${sha.substring(0, 7)}.`);
          resolve(true);
        } else {
          console.warn(`⚠️ Failed to post commit status (${res.statusCode}): ${body}`);
          resolve(false);
        }
      });
    });

    req.on('error', (err) => {
      console.warn(`⚠️ Error posting commit status: ${err.message}`);
      resolve(false);
    });

    req.write(payload);
    req.end();
  });
}

function runSinglePackage(targetDir, pkgName, configFile) {
  const thresholds = loadThresholds(configFile);
  let score = 0;
  let killed = 0;
  let total = 0;
  let foundReport = false;

  const jsonFiles = findJsonReports(targetDir);
  if (jsonFiles.length > 0) {
    try {
      const data = JSON.parse(fs.readFileSync(jsonFiles[0], 'utf8'));
      if (data.mutationScore !== undefined) {
        score = Number(data.mutationScore);
      }
      const files = data.files || {};
      for (const f of Object.values(files)) {
        for (const m of (f.mutants || [])) {
          const st = String(m.status || '').toLowerCase();
          if (st === 'killed' || st === 'timeout') {
            killed++;
            total++;
          } else if (st === 'survived' || st === 'nocoverage') {
            total++;
          }
        }
      }
      if (total > 0 && data.mutationScore === undefined) {
        score = Math.round((killed / total) * 10000) / 100;
      } else if (total === 0) {
        score = 100;
      }
      foundReport = true;
    } catch (err) {
      console.warn(`Error parsing report ${jsonFiles[0]}: ${err.message}`);
    }
  } else {
    console.warn(`No Stryker JSON reports found in directory: ${targetDir}`);
  }

  const passedGate = foundReport && (score >= thresholds.break || total === 0);
  const statusLabel = foundReport ? getStatusLabel(score, thresholds, total) : '❌ FAILED';

  const sha = process.env.GITHUB_SHA || 'unknown';
  const repo = process.env.GITHUB_REPOSITORY || '';
  const runId = process.env.GITHUB_RUN_ID || '';
  const serverUrl = process.env.GITHUB_SERVER_URL || 'https://github.com';
  const runUrl = repo && runId ? `${serverUrl}/${repo}/actions/runs/${runId}` : '';

  const metadata = {
    package: pkgName,
    commit_sha: sha,
    execution_date: new Date().toISOString(),
    mutation_score: score,
    mutants_killed: killed,
    total_mutants: total,
    threshold_high: thresholds.high,
    threshold_low: thresholds.low,
    threshold_break: thresholds.break,
    status: statusLabel,
    passed_break: passedGate,
    run_url: runUrl
  };

  fs.mkdirSync('StrykerOutput', { recursive: true });
  fs.writeFileSync(path.join('StrykerOutput', `summary-${pkgName}.json`), JSON.stringify(metadata, null, 2));

  // Write Step Summary
  const stepSummaryPath = process.env.GITHUB_STEP_SUMMARY;
  if (stepSummaryPath) {
    const summary = `
## 🛡️ Stryker Mutation Testing Results — ${pkgName}

| Metric | Value |
|--------|-------|
| **Mutation Score** | **${score}%** |
| **Mutants Killed** | ${killed} |
| **Total Mutants** | ${total} |
| **Threshold High** | ≥${thresholds.high}% |
| **Threshold Low** | ≥${thresholds.low}% |
| **Threshold Break** | ≥${thresholds.break}% |
| **Status** | ${statusLabel} |
| **Commit SHA** | \`${sha.substring(0, 7)}\` |
| **Execution date** | ${metadata.execution_date} |
`;
    fs.appendFileSync(stepSummaryPath, summary);
  }

  // Set GitHub Output
  const outputPath = process.env.GITHUB_OUTPUT;
  if (outputPath) {
    fs.appendFileSync(outputPath, `score=${score}\n`);
    fs.appendFileSync(outputPath, `passed_gate=${passedGate}\n`);
    fs.appendFileSync(outputPath, `status=${statusLabel}\n`);
    fs.appendFileSync(outputPath, `killed=${killed}\n`);
    fs.appendFileSync(outputPath, `total=${total}\n`);
  }

  console.log(`[${pkgName}] Stryker Score: ${score}% (${killed}/${total}) - ${statusLabel}`);

  if (!passedGate) {
    console.error(`❌ [${pkgName}] Mutation score ${score}% is below break threshold ${thresholds.break}%!`);
    process.exit(1);
  }
}

async function runAggregate(searchDir) {
  const targetDir = searchDir || 'StrykerOutput';
  const defaultThresholds = loadThresholds('stryker-config.json');

  let summaryFiles = [];
  function collectSummaries(dir) {
    if (!fs.existsSync(dir)) return;
    const entries = fs.readdirSync(dir, { withFileTypes: true });
    for (const entry of entries) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        collectSummaries(full);
      } else if (entry.name.startsWith('summary-') && entry.name.endsWith('.json')) {
        summaryFiles.push(full);
      }
    }
  }

  collectSummaries(targetDir);

  if (summaryFiles.length === 0) {
    console.error(`❌ No summary-*.json files found in ${targetDir}. Cannot evaluate quality gate.`);
    process.exit(1);
  }

  const summaries = [];
  let totalKilled = 0;
  let totalMutants = 0;
  let minScore = 100;
  let allPassed = true;
  let sha = process.env.GITHUB_SHA || 'unknown';
  let executionDate = new Date().toISOString();

  for (const file of summaryFiles) {
    try {
      const data = JSON.parse(fs.readFileSync(file, 'utf8'));
      summaries.push(data);
      totalKilled += Number(data.mutants_killed || 0);
      totalMutants += Number(data.total_mutants || 0);
      const score = Number(data.mutation_score || 0);
      if (score < minScore) minScore = score;
      if (!data.passed_break) allPassed = false;
      if (data.commit_sha && data.commit_sha !== 'unknown') sha = data.commit_sha;
      if (data.execution_date) executionDate = data.execution_date;
    } catch (e) {
      console.warn(`Failed reading summary file ${file}: ${e.message}`);
    }
  }

  const overallScore = totalMutants > 0 ? Math.round((totalKilled / totalMutants) * 10000) / 100 : 100;
  const overallStatus = allPassed
    ? getStatusLabel(overallScore, defaultThresholds, totalMutants)
    : '❌ FAILED';

  const repo = process.env.GITHUB_REPOSITORY || '';
  const [owner, repoName] = repo.split('/');
  const runId = process.env.GITHUB_RUN_ID || '';
  const serverUrl = process.env.GITHUB_SERVER_URL || 'https://github.com';
  const runUrl = repo && runId ? `${serverUrl}/${repo}/actions/runs/${runId}` : '';
  const token = process.env.GITHUB_TOKEN;

  const aggregateMetadata = {
    commit_sha: sha,
    execution_date: executionDate,
    overall_mutation_score: overallScore,
    min_package_score: minScore,
    total_mutants_killed: totalKilled,
    total_mutants: totalMutants,
    threshold_high: defaultThresholds.high,
    threshold_low: defaultThresholds.low,
    threshold_break: defaultThresholds.break,
    status: overallStatus,
    passed_break: allPassed,
    run_url: runUrl,
    packages: summaries
  };

  fs.mkdirSync('StrykerOutput', { recursive: true });
  fs.writeFileSync(path.join('StrykerOutput', 'aggregate-summary.json'), JSON.stringify(aggregateMetadata, null, 2));

  // Write Aggregated Step Summary
  const stepSummaryPath = process.env.GITHUB_STEP_SUMMARY;
  if (stepSummaryPath) {
    let tableRows = summaries.map(s => `| **${s.package}** | **${s.mutation_score}%** | ${s.mutants_killed} | ${s.total_mutants} | ≥${s.threshold_break}% | ${s.status} |`).join('\n');

    const summaryMd = `
# 🛡️ Stryker Mutation Testing — Aggregated Quality Gate

| Metric | Overall Value |
|--------|---------------|
| **Overall Mutation Score** | **${overallScore}%** |
| **Lowest Package Score** | **${minScore}%** |
| **Total Mutants Killed** | ${totalKilled} |
| **Total Mutants Evaluated** | ${totalMutants} |
| **Threshold High** | ≥${defaultThresholds.high}% |
| **Threshold Low** | ≥${defaultThresholds.low}% |
| **Threshold Break** | ≥${defaultThresholds.break}% |
| **Quality Gate Status** | ${overallStatus} |
| **Commit SHA** | \`${sha.substring(0, 7)}\` |
| **Execution Date** | ${executionDate} |

### 📦 Package Matrix Breakdown

| Package | Mutation Score | Killed | Total | Threshold Break | Status |
|---------|----------------|--------|-------|-----------------|--------|
${tableRows}
`;
    fs.appendFileSync(stepSummaryPath, summaryMd);
  }

  // Publish GitHub commit status
  if (token && owner && repoName && sha && sha !== 'unknown') {
    await postCommitStatus(owner, repoName, sha, token, {
      state: allPassed ? 'success' : 'failure',
      target_url: runUrl,
      description: `Score: ${overallScore}% (Min: ${minScore}%, Break: ≥${defaultThresholds.break}%)`
    });
  }

  console.log(`\n==================================================`);
  console.log(`STRYKER MUTATION QUALITY GATE: ${overallStatus}`);
  console.log(`Overall Score: ${overallScore}% | Min Package Score: ${minScore}%`);
  console.log(`Mutants Killed: ${totalKilled}/${totalMutants} | Passed: ${allPassed ? 'YES' : 'NO'}`);
  console.log(`==================================================\n`);

  if (!allPassed) {
    console.error(`❌ One or more packages failed the mutation break threshold (≥${defaultThresholds.break}%).`);
    process.exit(1);
  }
}

async function main() {
  const arg1 = process.argv[2] || 'StrykerOutput/ci';

  if (arg1 === '--aggregate') {
    const searchDir = process.argv[3] || 'StrykerOutput';
    await runAggregate(searchDir);
  } else {
    const targetDir = arg1;
    const pkgName = process.argv[3] || 'Transaction';
    const configFile = process.argv[4] || 'stryker-config.json';
    runSinglePackage(targetDir, pkgName, configFile);
  }
}

main();
