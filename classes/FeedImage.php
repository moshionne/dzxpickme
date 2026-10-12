<?php
require_once __DIR__ . '/Model.php';

class FeedImage extends Model
{
    public function getByAesthetic(?string $aesthetic, int $limit = 20): array
    {
        if ($aesthetic) {
            $stmt = $this->pdo->prepare(
                "SELECT id, aesthetic, category, image_path, title, description
                 FROM feed_images
                 WHERE aesthetic = ?
                 ORDER BY RANDOM()
                 LIMIT ?"
            );
            $stmt->bindValue(1, $aesthetic, PDO::PARAM_STR);
            $stmt->bindValue(2, $limit, PDO::PARAM_INT);
            $stmt->execute();
        } else {
            $stmt = $this->pdo->prepare(
                "SELECT id, aesthetic, category, image_path, title, description
                 FROM feed_images
                 ORDER BY RANDOM()
                 LIMIT ?"
            );
            $stmt->bindValue(1, $limit, PDO::PARAM_INT);
            $stmt->execute();
        }
        return $stmt->fetchAll();
    }

    public function getAesthetics(): array
    {
        $stmt = $this->pdo->query(
            "SELECT DISTINCT aesthetic FROM feed_images ORDER BY aesthetic ASC"
        );
        return $stmt->fetchAll(PDO::FETCH_COLUMN);
    }
}