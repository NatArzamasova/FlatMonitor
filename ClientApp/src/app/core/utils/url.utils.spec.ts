import { isHttpUrl } from './url.utils';

describe('url.utils', () => {
  describe('isHttpUrl', () => {
    it('accepts http and https URLs', () => {
      expect(isHttpUrl('https://example.com/flat1')).toBe(true);
      expect(isHttpUrl('http://example.com/flat1')).toBe(true);
      expect(isHttpUrl('HTTPS://EXAMPLE.COM')).toBe(true);
    });

    it('rejects script and data URLs', () => {
      expect(isHttpUrl('javascript:alert(1)')).toBe(false);
      expect(isHttpUrl('JavaScript:alert(1)')).toBe(false);
      expect(isHttpUrl(' javascript:alert(1)')).toBe(false);
      expect(isHttpUrl('data:text/html,<script>alert(1)</script>')).toBe(false);
      expect(isHttpUrl('vbscript:msgbox(1)')).toBe(false);
    });

    it('rejects other protocols', () => {
      expect(isHttpUrl('ftp://example.com/file')).toBe(false);
      expect(isHttpUrl('file:///etc/passwd')).toBe(false);
      expect(isHttpUrl('mailto:someone@example.com')).toBe(false);
    });

    it('rejects strings that are not absolute URLs', () => {
      expect(isHttpUrl('not-a-url')).toBe(false);
      expect(isHttpUrl('/relative/path')).toBe(false);
      expect(isHttpUrl('//example.com/flat1')).toBe(false);
    });

    it('rejects empty and undefined values', () => {
      expect(isHttpUrl('')).toBe(false);
      expect(isHttpUrl(undefined)).toBe(false);
    });
  });
});
