<?php
session_start();

if (!isset($_SESSION['user_id'])) {
    header("Location: ../log.php");
    exit;
}

require __DIR__ . '/../config/db.php';

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    header("Location: ../upload-outfit.php");
    exit;
}

$imageUrl = trim($_POST['image_url'] ?? '');
$title    = trim($_POST['title'] ?? '');
$desc     = trim($_POST['description'] ?? '');

// валидация
if ($imageUrl === '' || !filter_var($imageUrl, FILTER_VALIDATE_URL)) {
    $_SESSION['outfit_error'] = "Некорректная ссылка на картинку";
    header("Location: ../upload-outfit.php");
    exit;
}

if ($title === '') {
    $_SESSION['outfit_error'] = "Введите название";
    header("Location: ../upload-outfit.php");
    exit;
}

if (mb_strlen($title) > 200) {
    $_SESSION['outfit_error'] = "Название слишком длинное (макс. 200 символов)";
    header("Location: ../upload-outfit.php");
    exit;
}

// сохраняем в БД
try {
    $stmt = $pdo->prepare(
        "INSERT INTO outfits (user_id, title, description, image_url)
         VALUES (?, ?, ?, ?)"
    );
    $stmt->execute([$_SESSION['user_id'], $title, $desc, $imageUrl]);

    $_SESSION['outfit_success'] = "Образ сохранён!";
    header("Location: ../wardrobe.php");
    exit;
} catch (PDOException $e) {
    error_log('save_outfit error: ' . $e->getMessage());
    $_SESSION['outfit_error'] = "Ошибка сохранения. Попробуй ещё раз.";
    header("Location: ../upload-outfit.php");
    exit;
}