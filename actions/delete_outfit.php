<?php
session_start();
header('Content-Type: application/json');

if (!isset($_SESSION['user_id'])) {
    echo json_encode(['ok' => false, 'error' => 'Не авторизован']);
    exit;
}

require __DIR__ . '/../config/db.php';

$data = json_decode(file_get_contents('php://input'), true);
$outfitId = (int)($data['id'] ?? 0);

if ($outfitId <= 0) {
    echo json_encode(['ok' => false, 'error' => 'Неверный ID']);
    exit;
}

try {
    // снач получаем путь к файлу, чтобы удалить его
    $stmt = $pdo->prepare(
        "SELECT image_url FROM outfits WHERE id = ? AND user_id = ?"
    );
    $stmt->execute([$outfitId, $_SESSION['user_id']]);
    $outfit = $stmt->fetch();

    if (!$outfit) {
        echo json_encode(['ok' => false, 'error' => 'Образ не найден']);
        exit;
    }

    // удал из БД
    $stmt = $pdo->prepare(
        "DELETE FROM outfits WHERE id = ? AND user_id = ?"
    );
    $stmt->execute([$outfitId, $_SESSION['user_id']]);

    // попытка удалить файл (если он наш, локальный)
    $path = $outfit['image_url'];
    if (strpos($path, 'assets/img/outfits/') === 0) {
        $fullPath = __DIR__ . '/../' . $path;
        if (file_exists($fullPath)) {
            @unlink($fullPath);
        }
    }

    echo json_encode(['ok' => true]);
} catch (PDOException $e) {
    error_log('delete_outfit error: ' . $e->getMessage());
    echo json_encode(['ok' => false, 'error' => 'Ошибка удаления']);
}