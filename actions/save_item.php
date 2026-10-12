<?php
session_start();

if (!isset($_SESSION['user_id'])) {
    header("Location: ../log.php");
    exit;
}

require __DIR__ . '/../config/db.php';
require __DIR__ . '/../classes/ClothingItem.php';

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    header("Location: ../profile.php");
    exit;
}

$name     = trim($_POST['name'] ?? '');
$category = $_POST['category'] ?? 'top';
$imageUrl = trim($_POST['image_url'] ?? '');
$color    = trim($_POST['color'] ?? '');
if ($color === '') $color = null;

if ($name === '') {
    $_SESSION['upload_error'] = "Введите название";
    header("Location: ../profile.php");
    exit;
}

if ($imageUrl === '') {
    $_SESSION['upload_error'] = "Вставьте ссылку на картинку";
    header("Location: ../profile.php");
    exit;
}

if (!filter_var($imageUrl, FILTER_VALIDATE_URL)) {
    $_SESSION['upload_error'] = "Некорректная ссылка";
    header("Location: ../profile.php");
    exit;
}

$itemModel = new ClothingItem($pdo);
$itemModel->create(
    $_SESSION['user_id'],
    $name,
    $imageUrl,
    $category,
    $color
);

$_SESSION['upload_ok'] = "Вещь добавлена!";
header("Location: ../profile.php");
exit;