<?php
require_once __DIR__ . '/../vendor/autoload.php';

use Aws\S3\S3Client;
use Aws\Exception\AwsException;

class YandexStorage
{
    private S3Client $s3;
    private string $bucket;

    public function __construct(string $key, string $secret, string $bucket)
    {
        $this->bucket = $bucket;

        $this->s3 = new S3Client([
            'version'   => 'latest',
            'region'    => 'ru-central1',
            'endpoint'  => 'https://storage.yandexcloud.net',
            'credentials' => [
                'key'    => $key,
                'secret' => $secret,
            ],
            'signature' => 'v4',
            'use_path_style_endpoint' => false,
            'http' => [
                'verify' => 'C:/OSPanel/data/ssl/cacert.pem',
            ],
        ]);
    }

    public function upload(string $key, string $data, string $mime): string
    {
        try {
            $this->s3->putObject([
                'Bucket'      => $this->bucket,
                'Key'         => $key,
                'Body'        => $data,
                'ContentType' => $mime,
            ]);
        } catch (AwsException $e) {
            error_log('Yandex S3 error: ' . $e->getMessage());
            error_log('Yandex error code: ' . $e->getAwsErrorCode());
            error_log('Yandex error type: ' . $e->getAwsErrorType());
            error_log('Yandex status: ' . $e->getStatusCode());
            throw new Exception('S3 error: ' . $e->getAwsErrorMessage());
        }

        return 'https://' . $this->bucket . '.storage.yandexcloud.net/' . $key;
    }
}