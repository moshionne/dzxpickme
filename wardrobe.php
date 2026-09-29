<?php
session_start();
if (!isset($_SESSION['user_id'])) {
    header("Location: log.php");
    exit;
}
require __DIR__ . '/config/db.php';

$stmt = $pdo->prepare(
    "SELECT * FROM outfits WHERE user_id = ? ORDER BY id DESC"
);
$stmt->execute([$_SESSION['user_id']]);
$outfits = $stmt->fetchAll();
?>
<!DOCTYPE html>
<html lang="ru">
<head>
    <meta charset="UTF-8">
    <title>Шкаф</title>
    <link rel="stylesheet" href="css/style.css">
</head>
<body>
    <h1>Мой шкаф</h1>

    <p>
        <a href="index.php">Главная</a> |
        <a href="profile.php">Профиль</a> |
        <a href="logout.php">Выйти</a>
    </p>

    <?php if (empty($outfits)): ?>
        <p>Пока нет образов.</p>
    <?php else: ?>
        <?php foreach ($outfits as $outfit): ?>
            <div>
                <h3><?= htmlspecialchars($outfit['title']) ?></h3>
                <p><?= htmlspecialchars($outfit['description']) ?></p>
            </div>
        <?php endforeach; ?>
    <?php endif; ?>
</body>
</html>