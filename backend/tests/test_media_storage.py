from app.media_storage import R2MediaStorage


class FakeObjectBody:
    def read(self) -> bytes:
        return b"stored-image"


class FakeS3Client:
    def __init__(self):
        self.put_calls: list[dict] = []
        self.get_calls: list[dict] = []
        self.delete_calls: list[dict] = []

    def put_object(self, **kwargs) -> None:
        self.put_calls.append(kwargs)

    def get_object(self, **kwargs) -> dict:
        self.get_calls.append(kwargs)
        return {"Body": FakeObjectBody()}

    def delete_object(self, **kwargs) -> None:
        self.delete_calls.append(kwargs)


def test_r2_storage_writes_reads_and_deletes_private_objects():
    client = FakeS3Client()
    storage = R2MediaStorage(client=client, bucket="tarla-media")

    storage.save("leaf.jpg", b"stored-image", "image/jpeg")
    assert client.put_calls == [
        {
            "Bucket": "tarla-media",
            "Key": "leaf.jpg",
            "Body": b"stored-image",
            "ContentType": "image/jpeg",
        }
    ]

    assert storage.load("leaf.jpg") == b"stored-image"
    assert client.get_calls == [{"Bucket": "tarla-media", "Key": "leaf.jpg"}]

    storage.delete("leaf.jpg")
    assert client.delete_calls == [{"Bucket": "tarla-media", "Key": "leaf.jpg"}]
