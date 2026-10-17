<?php
session_start();
header('Content-Type: application/json');

if (!isset($_SESSION['user_id'])) {
    echo json_encode(['ok' => false, 'error' => 'Не авторизован']);
    exit;
}

require __DIR__ . '/../config/db.php';
require __DIR__ . '/../classes/YandexStorage.php';

$secrets = require __DIR__ . '/../secrets.php';

$data = json_decode(file_get_contents('php://input'), true);

$imageData = $data['image_data'] ?? '';
$title     = trim($data['title'] ?? '');
$desc      = trim($data['description'] ?? '');

if ($title === '') {
    echo json_encode(['ok' => false, 'error' => 'Введите название']);
    exit;
}

if (!preg_match('/^data:image\/(jpeg|png);base64,/', $imageData)) {
    echo json_encode(['ok' => false, 'error' => 'Некорректные данные изображения']);
    exit;
}

// деколируем в base64
$base64 = substr($imageData, strpos($imageData, ',') + 1);
$binary = base64_decode($base64);

if ($binary === false || strlen($binary) < 100) {
    echo json_encode(['ok' => false, 'error' => 'Не удалось декодировать изображение']);
    exit;
}

// загружаем в Yandex Object Storage
try {
    $storage = new YandexStorage(
        $secrets['yandex_key'],
        $secrets['yandex_secret'],
        $secrets['yandex_bucket']
    );

    $fileName = 'outfits/outfit_' . uniqid() . '.jpg';
    $imageUrl = $storage->upload($fileName, $binary, 'image/jpeg');
} catch (Exception $e) {
    error_log('Yandex upload error: ' . $e->getMessage());
    echo json_encode(['ok' => false, 'error' => 'Ошибка загрузки в облако']);
    exit;
}

// созраняем URL в БД
try {
    $stmt = $pdo->prepare(
        "INSERT INTO outfits (user_id, title, description, image_url)
         VALUES (?, ?, ?, ?)"
    );
    $stmt->execute([$_SESSION['user_id'], $title, $desc, $imageUrl]);

    echo json_encode(['ok' => true, 'image_url' => $imageUrl]);
} catch (PDOException $e) {
    error_log('save_built_outfit error: ' . $e->getMessage());
    echo json_encode(['ok' => false, 'error' => 'Ошибка сохранения']);
}