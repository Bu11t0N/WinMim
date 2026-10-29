"""Bounded HTTPS downloads for lab symbol acquisition; certificates remain verified."""
import socket
import ssl
import time
import urllib.error
import urllib.request


def read_https(url, *, timeout=90, limit=256 * 1024 * 1024, attempts=3):
    if not url.startswith('https://'):
        raise ValueError('HTTPS is required')
    if not 1 <= attempts <= 5 or limit < 1 or timeout <= 0:
        raise ValueError('Invalid download bounds')
    for attempt in range(attempts):
        try:
            request = urllib.request.Request(url, headers={'User-Agent': 'AeroForge-Lab/0.1'})
            with urllib.request.urlopen(request, timeout=timeout) as response:
                data = response.read(limit + 1)
            if len(data) > limit:
                raise ValueError('Download exceeds the configured size limit')
            return data
        except urllib.error.HTTPError as error:
            if error.code not in {408, 429, 500, 502, 503, 504}:
                raise
            failure = error
        except urllib.error.URLError as error:
            if isinstance(error.reason, ssl.SSLCertVerificationError):
                raise
            failure = error
        except (TimeoutError, socket.timeout, ConnectionError) as error:
            failure = error
        # Preserve the original error when all attempts have failed.
        if attempt == attempts - 1:
            raise failure
        time.sleep(2 ** attempt)
