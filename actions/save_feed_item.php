<?php
session_start();
header('Content-Type: application/json');

if (!isset($_SESSION['user_id'])) {
    echo json_encode(['ok' => false, 'error' => 'Не авторизован']);
    exit;
}

require __DIR__ . '/../config/db.php';
require __DIR__ . '/../classes/ClothingItem.php';

$data = json_decode(file_get_contents('php://input'), true);

$name      = trim($data['name'] ?? '');
$imageUrl  = trim($data['image_url'] ?? '');
$sourceUrl = trim($data['source_url'] ?? '');

if ($imageUrl === '' || !filter_var($imageUrl, FILTER_VALIDATE_URL)) {
    echo json_encode(['ok' => false, 'error' => 'Некорректная ссылка']);
    exit;
}

$itemModel = new ClothingItem($pdo);

try {
    $saved = $itemModel->save(
        $_SESSION['user_id'],
        $name !== '' ? $name : 'Без названия',
        $imageUrl,
        $sourceUrl !== '' ? $sourceUrl : null
    );
    echo json_encode(['ok' => true, 'saved' => $saved]);
} catch (Exception $e) {
    echo json_encode(['ok' => false, 'error' => $e->getMessage()]);
}