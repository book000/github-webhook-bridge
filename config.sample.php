<?php
$CONFIG = [
    // Secret to set to github (STRING)
    // If not set, specify NULL.
    // It is strongly recommended that you specify this for security reasons.
    "WEBHOOK_SECRET" => null,

    // Webhook URL to send after bridging (STRING)
    // e.g. Slack, Discord etc
    "WEBHOOK_URL" => "http://localhost:8080",

    // List of repositories that do not bridge (ARRAY)
    "EXCLUDE_REPOSITORYS" => [
        "book000/github-webhook-bridge",
    ],

    // List of bridging repositories (ARRAY)
    "INCLUDE_REPOSITORYS" => null,

    // Log file directory path (STRING)
    // If you do not want to output a log, specify NULL
    // e.g. "LOG_DIR" => "/tmp/github-webhook-bridge/",
    // Log file is output to {REPOSITORY_NAME}/{DATETIME}.log
    // By default, it is created in the logs directory.
    "LOG_DIR" => __DIR__ . "/logs/",

    // Time zone (STRING)
    // If the time zone is not correct and want to change due to the setting in php.ini etc.
    // If not set, specify NULL.
    // List of Supported Timezones: https://www.php.net/manual/en/timezones.php
    "TIME_ZONE" => "UTC",

    // Debug mode (BOOLEAN)
    "DEBUG" => false,
];
