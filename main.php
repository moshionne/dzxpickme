<?php
session_start();
require __DIR__ . '/config/db.php';
require __DIR__ . '/classes/ClothingItem.php';
require __DIR__ . '/classes/PinterestScraper.php';

$secrets = require __DIR__ . '/config/secrets.php';

// поисковый запрос
$query = trim($_GET['q'] ?? 'y2k outfit');

// получаем картинки из пинтереста через апи
$pins = [];
if (!empty($secrets['apify_token'])) {
    $scraper = new PinterestScraper($secrets['apify_token']);
    $pins = $scraper->search($query, 20);
}

// сохр пользователя (для подсветки кнопок)
$savedUrls = [];
if (isset($_SESSION['user_id'])) {
    $itemModel = new ClothingItem($pdo);
    $saved = $itemModel->getAllByUser($_SESSION['user_id']);
    foreach ($saved as $s) {
        $savedUrls[$s['image_url']] = true;
    }
}

$isLoggedIn = isset($_SESSION['user_id']);
$username   = $_SESSION['username'] ?? 'Гость';
?>
<!DOCTYPE html>
<html lang="ru">
<head>
    <meta charset="UTF-8">
    <title>Лента</title>
    <link rel="stylesheet" href="assets/css/style-main.css">
</head>
<body>

<header class="header">
    <div class="slider" id="slider">
        <div class="slidertrack" id="track">
            <a href="main.php?q=y2k outfit" class="slidercard slidercard--y2k"
               style="background-image: url('assets/img/y2k.jpg')">
                <div class="slidercard__overlay">
                    <div class="slidercard__content">
                        <h2>Y2K</h2>
                        <p>Блеск, низкая посадка, 2000-е</p>
                    </div>
                </div>
            </a>

            <a href="main.php?q=coquette outfit" class="slidercard slidercard--coquette"
               style="background-image: url('assets/img/coquette.jpg')">
                <div class="slidercard__overlay">
                    <div class="slidercard__content">
                        <h2>Coquette</h2>
                        <p>Кружево, банты, пастель</p>
                    </div>
                </div>
            </a>

            <a href="main.php?q=old money outfit" class="slidercard slidercard--oldmoney"
               style="background-image: url('assets/img/oldmoney.jpg')">
                <div class="slidercard__overlay">
                    <div class="slidercard__content">
                        <h2>Old Money</h2>
                        <p>Классика, твид, элегантность</p>
                    </div>
                </div>
            </a>
        </div>
        <button class="sliderbtn sliderbtn--prev" id="prev">‹</button>
        <button class="sliderbtn sliderbtn--next" id="next">›</button>
        <div class="sliderdots" id="dots"></div>
    </div>

    <aside class="profile">
        <div class="profileavatar"></div>
        <div class="profilename"><?= htmlspecialchars($username) ?></div>

        <ul class="links">
            <li><a href="index.php">меню</a></li>
            <li><a href="profile.php">профиль</a></li>
            <li><a href="wardrobe.php">шкаф</a></li>
            <?php if ($isLoggedIn): ?>
                <li><a href="logout.php">выйти</a></li>
            <?php else: ?>
                <li><a href="log.php">войти</a></li>
            <?php endif; ?>
        </ul>

        <form method="GET" style="margin-top: 20px; display: flex; gap: 6px;">
            <input type="text" name="q" placeholder="y2k outfit, coquette..."
                   value="<?= htmlspecialchars($query) ?>"
                   style="padding: 8px; border-radius: 8px; border: 1px solid #ddd; flex: 1;">
            <button type="submit" style="padding: 8px 12px; border-radius: 8px; border: none; background: #e94b83; color: #fff; cursor: pointer;">
                Найти
            </button>
        </form>
    </aside>
</header>

<main class="feed" id="feed"></main>

<script src="https://unpkg.com/imagesloaded@5/imagesloaded.pkgd.min.js"></script>
<script src="https://unpkg.com/masonry-layout@4/dist/masonry.pkgd.min.js"></script>
<script>
    const pinsData = <?= json_encode($pins, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) ?>;
    const isLoggedIn = <?= $isLoggedIn ? 'true' : 'false' ?>;
    const savedUrls = <?= json_encode(array_keys($savedUrls), JSON_UNESCAPED_SLASHES) ?>;

    //слайдер
    const track = document.getElementById('track');
    const dotsWrap = document.getElementById('dots');
    const cards = track.children;
    let current = 0;
    const total = cards.length;

    for (let i = 0; i < total; i++) {
        const dot = document.createElement('button');
        dot.className = 'slider__dot' + (i === 0 ? ' active' : '');
        dot.addEventListener('click', () => goTo(i));
        dotsWrap.appendChild(dot);
    }
    function update() {
        track.style.transform = `translateX(-${current * 100}%)`;
        [...dotsWrap.children].forEach((d, i) =>
            d.classList.toggle('active', i === current)
        );
    }
    function goTo(i) {
        current = (i + total) % total;
        update();
    }
    document.getElementById('prev').addEventListener('click', () => goTo(current - 1));
    document.getElementById('next').addEventListener('click', () => goTo(current + 1));

    // лента
    const feedContainer = document.getElementById('feed');

    if (pinsData.length === 0) {
        feedContainer.innerHTML = '<p style="text-align:center; padding:40px; color:#6b525e;">Ничего не найдено. Попробуй другой запрос.</p>';
    } else {
        pinsData.forEach((item, index) => {
            const isSaved = savedUrls.includes(item.img);
            const card = document.createElement('article');
            card.className = 'pin-card';
            card.innerHTML = `
                <div class="pin-card__wrapper">
                    <img src="${item.img}" alt="${item.title}" loading="lazy">
                    <button class="save-btn ${isSaved ? 'save-btn--saved' : ''}" data-index="${index}">
                        ${isSaved ? 'Сохранено' : 'Сохранить'}
                    </button>
                </div>
                <div class="pin-card__info">
                    <div class="pin-card__title">${item.title}</div>
                    <div class="pin-card__desc">${item.desc}</div>
                </div>
            `;
            feedContainer.appendChild(card);
        });

        imagesLoaded(feedContainer, function () {
            new Masonry(feedContainer, {
                itemSelector: '.pin-card',
                columnWidth: '.pin-card',
                gutter: 16,
                percentPosition: true
            });
        });
    }

    // кнпк сохранить
    feedContainer.addEventListener('click', async (e) => {
        const btn = e.target.closest('.save-btn');
        if (!btn) return;

        if (!isLoggedIn) {
            window.location.href = 'log.php';
            return;
        }

        const item = pinsData[btn.dataset.index];
        const wasSaved = btn.classList.contains('save-btn--saved');
        const action = wasSaved ? 'unsave_item.php' : 'save_feed_item.php';
        const endpoint = 'actions/' + action;

        btn.disabled = true;
        const originalText = btn.textContent.trim();
        btn.textContent = 'Сохранение...';

        try {
            const response = await fetch(endpoint, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    name: item.title,
                    image_url: item.img,
                    source_url: item.source_url || ''
                })
            });
            const result = await response.json();

            if (result.ok) {
                if (wasSaved) {
                    btn.classList.remove('save-btn--saved');
                    btn.textContent = 'Сохранить';
                } else {
                    btn.classList.add('save-btn--saved');
                    btn.textContent = 'Сохранено';
                }
            } else {
                btn.textContent = originalText;
                console.error(result.error);
            }
        } catch (err) {
            btn.textContent = originalText;
            console.error(err);
        } finally {
            btn.disabled = false;
        }
    });
</script>
</body>
</html>