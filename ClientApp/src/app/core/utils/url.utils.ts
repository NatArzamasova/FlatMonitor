const ALLOWED_PROTOCOLS = ['http:', 'https:'];

export function isHttpUrl(url: string | undefined): url is string {
  if (!url) {
    return false;
  }
  try {
    return ALLOWED_PROTOCOLS.includes(new URL(url).protocol);
  } catch {
    return false;
  }
}
