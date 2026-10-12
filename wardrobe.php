<?php
session_start();
if (!isset($_SESSION['user_id'])) {
    header("Location: log.php");
    exit;
}
require __DIR__ . '/config/db.php';

$stmt = $pdo->prepare(
    "SELECT id, title, description, image_url
     FROM outfits
     WHERE user_id = ?
     ORDER BY id DESC"
);
$stmt->execute([$_SESSION['user_id']]);
$outfits = $stmt->fetchAll();
?>
<!DOCTYPE html>
<html lang="ru">
<head>
    <meta charset="UTF-8">
    <title>Мой шкаф</title>
    <link rel="stylesheet" href="assets/css/style-profile.css">
</head>
<body>

<div class="profile-header">
    <div class="profile-header__info">
        <div class="profileavatar"></div>
        <div class="profile-header__text">
            <h1 class="profilename">Мой шкаф</h1>
            <ul class="links">
                <li><a href="index.php">меню</a></li>
                <li><a href="main.php">лента</a></li>
                <li><a href="profile.php">профиль</a></li>
                <li><a href="choice.php">+ создать образ</a></li>
                <li><a href="logout.php">выйти</a></li>
            </ul>
        </div>
    </div>
</div>

<main class="feed">
    <?php if (empty($outfits)): ?>
        <div class="saved-empty">
            У тебя пока нет образов. <a href="choice.php">Создай первый!</a>
        </div>
    <?php else: ?>
        <div id="saved-feed">
            <?php foreach ($outfits as $outfit): ?>
                <article class="pin-card" data-id="<?= (int)$outfit['id'] ?>">
                    <div class="pin-card__wrapper">
                        <img src="<?= htmlspecialchars($outfit['image_url']) ?>"
                             alt="<?= htmlspecialchars($outfit['title']) ?>" loading="lazy">
                        <button class="save-btn save-btn--saved delete-outfit-btn"
                                data-id="<?= (int)$outfit['id'] ?>">
                            Удалить
                        </button>
                    </div>
                    <div class="pin-card__info">
                        <div class="pin-card__title"><?= htmlspecialchars($outfit['title']) ?></div>
                        <div class="pin-card__desc"><?= htmlspecialchars($outfit['description']) ?></div>
                    </div>
                </article>
            <?php endforeach; ?>
        </div>
    <?php endif; ?>
</main>

<script src="https://unpkg.com/imagesloaded@5/imagesloaded.pkgd.min.js"></script>
<script src="https://unpkg.com/masonry-layout@4/dist/masonry.pkgd.min.js"></script>
<script>
const feedContainer = document.getElementById('saved-feed');
if (feedContainer && typeof imagesLoaded !== 'undefined' && typeof Masonry !== 'undefined') {
    imagesLoaded(feedContainer, function () {
        new Masonry(feedContainer, {
            itemSelector: '.pin-card',
            columnWidth: 300,
            gutter: 16,
            fitWidth: true
        });
    });
}

//удаление образа
document.addEventListener('click', async (e) => {
    const btn = e.target.closest('.delete-outfit-btn');
    if (!btn) return;

    if (!confirm('Удалить этот образ?')) return;

    btn.disabled = true;
    const originalText = btn.textContent.trim();
    btn.textContent = 'Удаление...';

    try {
        const response = await fetch('actions/delete_outfit.php', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ id: parseInt(btn.dataset.id) })
        });
        const result = await response.json();

        if (result.ok) {
            const card = btn.closest('.pin-card');
            card.remove();
            if (feedContainer && typeof Masonry !== 'undefined') {
                const msnry = Masonry.data(feedContainer);
                if (msnry) msnry.reloadItems();
                if (msnry) msnry.layout();
            }
        } else {
            alert('Ошибка: ' + result.error);
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