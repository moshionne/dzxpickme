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
$imageUrl = trim($data['image_url'] ?? '');

if ($imageUrl === '') {
    echo json_encode(['ok' => false, 'error' => 'Пустой URL']);
    exit;
}

$itemModel = new ClothingItem($pdo);

try {
    $itemModel->delete($_SESSION['user_id'], $imageUrl);
    echo json_encode(['ok' => true]);
} catch (Exception $e) {
    echo json_encode(['ok' => false, 'error' => $e->getMessage()]);
}