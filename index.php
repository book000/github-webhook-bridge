<?php
require_once(__DIR__ . "/config.php");

if ($CONFIG["DEBUG"]) {
    ini_set('display_errors', 1);
    error_reporting(-1);
}

$LOG_PATH = null;

if ($CONFIG["TIME_ZONE"] != null && date_default_timezone_get() == $CONFIG["TIME_ZONE"]) {
    date_default_timezone_set($CONFIG["TIME_ZONE"]);
}

// https://www.php.net/manual/ja/function.getallheaders.php#84262
if (!function_exists("getallheaders")) {
    function getallheaders()
    {
        $headers = [];
        foreach ($_SERVER as $name => $value) {
            if (substr($name, 0, 5) == "HTTP_") {
                $headers[str_replace(" ", "-", ucwords(strtolower(str_replace("_", " ", substr($name, 5)))))] = $value;
            }
        }
        return $headers;
    }
}

/**
 * Output text to log
 *
 * @param string $str
 * @return void
 */
function writeLog($str)
{
    global $_SERVER, $LOG_PATH;
    $datetime = date("Y-m-d H:i:s");

    echo "[$datetime] $str\n";

    if ($LOG_PATH == null) {
        return;
    }
    file_put_contents($LOG_PATH, "[$datetime] {$_SERVER["REMOTE_ADDR"]} $str\n", FILE_APPEND | LOCK_EX);
}


$body = file_get_contents("php://input");
$payload = json_decode($body, true);
if (json_last_error() != JSON_ERROR_NONE) {
    http_response_code(400);
    writeLog("Parse JSON failed.");
    exit;
}

$owner = isset($payload["repository"]["owner"]["login"]) ?
    $payload["repository"]["owner"]["login"] :
    null;
$repository = isset($payload["repository"]["name"]) ?
    $payload["repository"]["name"] :
    null;

if ($CONFIG["LOG_DIR"] != null) {
    if (!file_exists($CONFIG["LOG_DIR"])) {
        mkdir($CONFIG["LOG_DIR"], 0777, true);
    }
    if (!is_writable($CONFIG["LOG_DIR"]) || !is_dir($CONFIG["LOG_DIR"])) {
        http_response_code(500);
        writeLog("You do not have permission to write to the log directory.");
        exit;
    }

    if ($owner != null && $repository != null) {
        $LOG_REPO_DIR = $CONFIG["LOG_DIR"] . "/" . basename($owner) . "/" . basename($repository);
    } else {
        $LOG_REPO_DIR = $CONFIG["LOG_DIR"] . "/NULL/";
    }

    if (!file_exists($LOG_REPO_DIR)) {
        mkdir($LOG_REPO_DIR, 0777, true);
    }
    if (!is_writable($LOG_REPO_DIR) || !is_dir($LOG_REPO_DIR)) {
        http_response_code(500);
        writeLog("You do not have permission to write to the log repository directory.");
        exit;
    }

    $LOG_PATH = $LOG_REPO_DIR . "/" . date("Ymd") . ".log";
}

if ($_SERVER["REQUEST_METHOD"] != "POST") {
    http_response_code(405);
    writeLog("Method not allowed.");
    exit;
}

$headers = getallheaders();

// Secret check
if ($CONFIG["WEBHOOK_SECRET"] != null) {
    $hmac = hash_hmac("sha1", file_get_contents("php://input"), $CONFIG["WEBHOOK_SECRET"]);

    if (!isset($headers["X-Hub-Signature"])) {
        http_response_code(400);
        writeLog("Secret key not found.");
        exit;
    }
    if (isset($headers["X-Hub-Signature"]) && ($headers["X-Hub-Signature"] != "sha1=" . $hmac)) {
        http_response_code(400);
        writeLog("Secret key error.");
        exit;
    }
}

$repo_name = $payload["repository"]["full_name"];
if ($CONFIG["EXCLUDE_REPOSITORYS"] != null && in_array($repo_name, $CONFIG["EXCLUDE_REPOSITORYS"])) {
    http_response_code(400);
    writeLog("This repository is excluded.");
    exit;
}
if ($CONFIG["INCLUDE_REPOSITORYS"] != null && !in_array($repo_name, $CONFIG["INCLUDE_REPOSITORYS"])) {
    http_response_code(400);
    writeLog("This repository is not a bridged repository.");
    exit;
}

if ($CONFIG["DEBUG"]) {
    print_r($headers);
}

if (isset($CONFIG["DISABLE_DEPENDABOT"]) && $CONFIG["DISABLE_DEPENDABOT"]) {
    if (isset($payload["pull_request"]) && $payload["pull_request"]["user"]["id"] == "49699333") {
        http_response_code(400);
        writeLog("Dependabot disabled (pull_request).");
        exit;
    } elseif (isset($payload["issue"]) && $payload["issue"]["user"]["id"] == "49699333") {
        http_response_code(400);
        writeLog("Dependabot disabled (issue).");
        exit;
    }
}

$curl = curl_init();
curl_setopt($curl, CURLOPT_URL, $CONFIG["WEBHOOK_URL"]);
curl_setopt($curl, CURLOPT_CUSTOMREQUEST, "POST");
curl_setopt($curl, CURLOPT_POSTFIELDS, $body);
curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, true);
curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
curl_setopt($curl, CURLOPT_HTTPHEADER, [
    "Accept: */*",
    "Content-Type: application/json",
    "User-Agent: " . $headers["User-Agent"],
    "X-Github-Delivery: " . $headers["X-Github-Delivery"],
    "X-Github-Event: " . $headers["X-Github-Event"],
    "X-Github-Hook-Id: " . $headers["X-Github-Hook-Id"],
    "X-Github-Hook-Installation-Target-Id: " . $headers["X-Github-Hook-Installation-Target-Id"],
    "X-Github-Hook-Installation-Target-Type: " . $headers["X-Github-Hook-Installation-Target-Type"],
    "X-Hub-Signature: " . $headers["X-Hub-Signature"],
    "X-Hub-Signature-256: " . $headers["X-Hub-Signature-256"]
]);
curl_setopt($curl, CURLOPT_HEADER, true);
$response = curl_exec($curl);

$response_code = curl_getinfo($curl, CURLINFO_RESPONSE_CODE);
$header_size = curl_getinfo($curl, CURLINFO_HEADER_SIZE);
$response_header = substr($response, 0, $header_size);
$response_body = substr($response, $header_size);
curl_close($curl);

writeLog("Webhook response code: " . $response_code);
writeLog("Webhook response header: " . $response_header);
writeLog("Webhook response body: " . $response_body);

if ($response_code <= 299) {
    writeLog("Successfully sent.");
} else {
    writeLog("Failed sent.");
}
