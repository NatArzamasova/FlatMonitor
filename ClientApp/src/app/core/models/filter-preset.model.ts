/** Mirrors the backend's FilterPresetDto. An empty `districts` list means all districts. */
export interface FilterPreset {
  id: number;
  name: string;
  districts: string[];
  isDefault: boolean;
  updatedAtUtc: string;
}

/** Body for creating or updating a preset; the backend trims the name and normalises the districts. */
export interface SaveFilterPresetRequest {
  name: string;
  districts: string[];
}
