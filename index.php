<?php
session_start();

$isLoggedIn = isset($_SESSION['user_id']);
$username   = $_SESSION['username'] ?? 'Гость';
?>
<!DOCTYPE html>
<html lang="ru">
<head>
    <meta charset="UTF-8">
    <title>Fashion App — Меню</title>
    <link rel="stylesheet" href="assets/css/style-index.css">
</head>
<body>
    <div class="menu">
        <h1>Fashion App</h1>
        <p>Привет, <?= htmlspecialchars($username) ?>!</p>

        <a href="main.php">Лента</a>

        <?php if ($isLoggedIn): ?>
            <a href="profile.php">Мой профиль</a>
            <a href="wardrobe.php">Шкаф</a>
            <a href="choice.php">Создать образ</a>
            <a href="logout.php" class="secondary">Выйти</a>
        <?php else: ?>
            <a href="log.php" class="secondary">Войти</a>
            <a href="reg.php" class="secondary">Регистрация</a>
        <?php endif; ?>
    </div>
</body>
</html>