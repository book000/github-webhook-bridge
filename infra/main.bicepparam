using 'main.bicep'

param githubWebhookSecret = readEnvironmentVariable('GITHUB_WEBHOOK_SECRET', '')
param githubUserMapBlob = readEnvironmentVariable('GITHUB_USER_MAP_BLOB', '')
param mutesBlob = readEnvironmentVariable('MUTES_BLOB', '')