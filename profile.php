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
    <link rel="stylesheet" href="assets/css/style-profile.css">
    <script src="https://unpkg.com/imagesloaded@5/imagesloaded.pkgd.min.js"></script>
    <script src="https://unpkg.com/masonry-layout@4/dist/masonry.pkgd.min.js"></script>
</head>
<body>

<div class="profile-header">
    <div class="profile-header__info">
        <div class="profileavatar"></div>
        <div class="profile-header__text">
            <h1 class="profilename"><?= htmlspecialchars($_SESSION['username']) ?></h1>
            <ul class="links">
                <li><a href="index.php">меню</a></li>
                <li><a href="main.php">лента</a></li>
                <li><a href="wardrobe.php">шкаф</a></li>
                <li><a href="choice.php">+ создать образ</a></li>
                <li><a href="logout.php">выйти</a></li>
            </ul>
        </div>
    </div>
</div>

<main class="feed">
    <h2 class="feed-title">Сохранённые карточки</h2>

    <?php if (empty($items)): ?>
        <div class="saved-empty">
            Пока ничего не сохранено. Зайди в <a href="main.php">ленту</a> и нажми «Сохранить» на карточке.
        </div>
    <?php else: ?>
        <div id="saved-feed">
            <?php foreach ($items as $item): ?>
                <article class="pin-card">
                    <div class="pin-card__wrapper">
                        <img src="<?= htmlspecialchars($item['image_url']) ?>"
                             alt="<?= htmlspecialchars($item['name']) ?>" loading="lazy">
                        <button class="save-btn save-btn--saved" data-url="<?= htmlspecialchars($item['image_url']) ?>">
                            Удалить
                        </button>
                    </div>
                    <div class="pin-card__info">
                        <div class="pin-card__title"><?= htmlspecialchars($item['name']) ?></div>
                    </div>
                </article>
            <?php endforeach; ?>
        </div>
    <?php endif; ?>
</main>

<script>
    const savedContainer = document.getElementById('saved-feed');
    if (savedContainer && typeof imagesLoaded !== 'undefined' && typeof Masonry !== 'undefined') {
        imagesLoaded(savedContainer, function () {
            new Masonry(savedContainer, {
                itemSelector: '.pin-card',
                columnWidth: 300,           // ← явное число, не селектор
                gutter: 16,
                fitWidth: true,
            });
        });
    }

    // цдвление сохр картинок
    document.addEventListener('click', async (e) => {
        const btn = e.target.closest('.save-btn');
        if (!btn || !btn.dataset.url) return;

        btn.disabled = true;
        const originalText = btn.textContent.trim();
        btn.textContent = 'Удаление...';

        try {
            const response = await fetch('actions/unsave_item.php', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ image_url: btn.dataset.url })
            });
            const result = await response.json();
            if (result.ok) {
                btn.closest('.pin-card').remove();
            } else {
                btn.textContent = originalText;
                btn.disabled = false;
            }
        } catch (err) {
            console.error(err);
            btn.textContent = originalText;
            btn.disabled = false;
        }
    });
</script>
</body>
</html>