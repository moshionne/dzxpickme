<?php
// прокси для картинок. обходит CORS
// принимает ?url=... и отдаёт байты картинки

$url = $_GET['url'] ?? '';

if ($url === '' || !filter_var($url, FILTER_VALIDATE_URL)) {
    http_response_code(400);
    exit('Bad request');
}

// разрешаем только безопасные домены 
$allowedHosts = [
    'i.pinimg.com',
    'pinimg.com',
    'cdn.dummyjson.com',
    'dummyjson.com',
];

$host = parse_url($url, PHP_URL_HOST);
$allowed = false;
foreach ($allowedHosts as $a) {
    if ($host === $a || str_ends_with($host, '.' . $a)) {
        $allowed = true;
        break;
    }
}

if (!$allowed) {
    http_response_code(403);
    exit('Forbidden');
}

// скачиваем картинку через cURL
$ch = curl_init($url);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
curl_setopt($ch, CURLOPT_TIMEOUT, 10);
curl_setopt($ch, CURLOPT_FOLLOWLOCATION, true);
curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, true);
curl_setopt($ch, CURLOPT_CAINFO, 'C:/OSPanel/data/ssl/cacert.pem');
curl_setopt($ch, CURLOPT_USERAGENT, 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36');

$data = curl_exec($ch);
$httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
$contentType = curl_getinfo($ch, CURLINFO_CONTENT_TYPE);
curl_close($ch);

if ($httpCode !== 200 || $data === false) {
    http_response_code(502);
    exit('Failed to fetch image');
}

// отдаём картинку с нашего домена
header('Content-Type: ' . ($contentType ?: 'image/jpeg'));
header('Cache-Control: public, max-age=86400');
header('Access-Control-Allow-Origin: *');
echo $data;