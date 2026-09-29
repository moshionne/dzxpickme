<?php
session_start();
if (!isset($_SESSION['user_id'])) {
    header("Location: log.php");
    exit;
}
require __DIR__ . '/config/db.php';
require __DIR__ . '/classes/ClothingItem.php';

$itemModel = new ClothingItem($pdo);
$items = $itemModel->getAllByUser($_SESSION['user_id']);
?>
<!DOCTYPE html>
<html lang="ru">
<head>
    <meta charset="UTF-8">
    <title>Профиль</title>
    <link rel="stylesheet" href="css/style.css">
</head>
<body>
    <h1>Профиль: <?= htmlspecialchars($_SESSION['username']) ?></h1>

    <p>
        <a href="index.php">Главная</a> |
        <a href="wardrobe.php">Шкаф</a> |
        <a href="logout.php">Выйти</a>
    </p>

    <h2>Мои вещи</h2>

    <?php if (empty($items)): ?>
        <p>Пока ничего не добавлено.</p>
    <?php else: ?>
        <?php foreach ($items as $item): ?>
            <div>
                <img src="<?= htmlspecialchars($item['image_path']) ?>"
                     alt="<?= htmlspecialchars($item['name']) ?>"
                     width="150">
                <p><?= htmlspecialchars($item['name']) ?></p>
            </div>
        <?php endforeach; ?>
    <?php endif; ?>
</body>
</html>