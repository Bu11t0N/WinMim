import importlib.util
import io
from pathlib import Path
import unittest
from unittest.mock import patch
import urllib.error

spec=importlib.util.spec_from_file_location('http_retry',Path(__file__).parents[1]/'tools/http_retry.py')
http=importlib.util.module_from_spec(spec);spec.loader.exec_module(http)


class HttpsRetryTests(unittest.TestCase):
    def test_timeout_then_success(self):
        with patch.object(http.urllib.request,'urlopen',side_effect=[TimeoutError('slow'),io.BytesIO(b'pdb')]) as get, patch.object(http.time,'sleep') as sleep:
            self.assertEqual(http.read_https('https://example.test/file'),b'pdb')
            self.assertEqual(get.call_count,2)
            sleep.assert_called_once_with(1)

    def test_permanent_http_failure_not_retried(self):
        missing=urllib.error.HTTPError('https://example.test/file',404,'missing',{},None)
        with patch.object(http.urllib.request,'urlopen',side_effect=missing) as get, patch.object(http.time,'sleep') as sleep:
            with self.assertRaises(urllib.error.HTTPError) as caught:http.read_https('https://example.test/file')
            self.assertIs(caught.exception,missing)
            get.assert_called_once();sleep.assert_not_called()
        missing.close()

    def test_retry_bound_and_size_limit(self):
        with patch.object(http.urllib.request,'urlopen',side_effect=TimeoutError('slow')) as get, patch.object(http.time,'sleep'):
            with self.assertRaises(TimeoutError):http.read_https('https://example.test/file')
            self.assertEqual(get.call_count,3)
        with patch.object(http.urllib.request,'urlopen',return_value=io.BytesIO(b'large')) as get:
            with self.assertRaisesRegex(ValueError,'size limit'):http.read_https('https://example.test/file',limit=2)
            get.assert_called_once()


if __name__=='__main__':unittest.main()
