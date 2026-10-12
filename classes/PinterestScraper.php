<?php
require_once __DIR__ . '/../vendor/autoload.php';

use Apify\Client\ApifyClient;

class PinterestScraper
{
    private ApifyClient $client;
    private string $actorId;

    public function __construct(string $token)
    {
        $this->client  = new ApifyClient($token);
        // нужный актор
        $this->actorId = 'themineworks/pinterest-search-scraper';
    }

    public function search(string $query, int $limit = 20): array
    {
        try {
            $run = $this->client->actor($this->actorId)->call([
                'searchQueries' => [$query],   // поиск по ключу
                'maxItems'      => $limit,
            ]);

            $datasetId = $run->getDefaultDatasetId();
            if (!$datasetId) {
                return [];
            }

            $items = $this->client->dataset($datasetId)->listItems();

            $results = [];
            foreach ($items as $item) {
                // у этого актора картинка лежит в image_url
                $imageUrl = $item['image_url'] ?? null;
                if (!$imageUrl) {
                    continue;
                }

                $results[] = [
                    'title'      => $item['title'] ?? 'Pinterest pin',
                    'desc'       => $item['description'] ?? '',
                    'img'        => $imageUrl,
                    'source_url' => $item['destination_url'] ?? '',
                ];
            }

            return $results;

        } catch (Exception $e) {
            error_log('Apify error: ' . $e->getMessage());
            return [];
        }
    }
}