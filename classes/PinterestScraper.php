<?php
require_once __DIR__ . '/../vendor/autoload.php';

use Apify\Client\ApifyClient;

class PinterestScraper
{
    private ApifyClient $client;
    private string $actorId;
    private string $token;

    public function __construct(string $token)
    {
        $this->client  = new ApifyClient($token);
        $this->actorId = 'themineworks/pinterest-search-scraper';
        $this->token   = $token;
    }

    public function search(string $query, int $limit = 20): array
    {
        set_time_limit(120); // Даем скрипту 2 минуты

        try {
            // 1. Запускаем актора через SDK (это работало, Run ID возвращался)
            $run = $this->client->actor($this->actorId)->call([
                'searchQueries' => [$query],
                'maxItems'      => $limit,
            ]);

            $datasetId = $run->getDefaultDatasetId();
            if (!$datasetId) {
                error_log('PinterestScraper: Dataset ID is empty');
                return [];
            }

            // 2. Забираем данные НАПРЯМУЮ через cURL (обходим зависание SDK)
            $url = "https://api.apify.com/v2/datasets/{$datasetId}/items?token={$this->token}&limit={$limit}&format=json";

            $ch = curl_init($url);
            curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
            curl_setopt($ch, CURLOPT_TIMEOUT, 30);
            curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, true);
            
            // Укажи свой путь к сертификату, если он другой
            curl_setopt($ch, CURLOPT_CAINFO, 'C:/OSPanel/data/ssl/cacert.pem'); 
            curl_setopt($ch, CURLOPT_USERAGENT, 'Mozilla/5.0');

            $response = curl_exec($ch);
            $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
            $curlError = curl_error($ch);
            curl_close($ch);

            if ($curlError) {
                error_log('cURL Error in PinterestScraper: ' . $curlError);
                return [];
            }

            if ($httpCode !== 200) {
                error_log('HTTP Error in PinterestScraper: ' . $httpCode . ' Response: ' . substr($response, 0, 200));
                return [];
            }

            // 3. Парсим JSON
            $items = json_decode($response, true);
            if (!is_array($items)) {
                error_log('PinterestScraper: Invalid JSON');
                return [];
            }

            // 4. Формируем результат
            $results = [];
            foreach ($items as $item) {
                $imageUrl = $item['image_url'] ?? null;
                if (!$imageUrl) continue;

                $results[] = [
                    'title'      => $item['title'] ?? 'Pinterest pin',
                    'desc'       => $item['description'] ?? '',
                    'img'        => $imageUrl,
                    'source_url' => $item['destination_url'] ?? '',
                ];
            }

            return $results;

        } catch (Exception $e) {
            error_log('Apify catch error: ' . $e->getMessage());
            return [];
        }
    }
}