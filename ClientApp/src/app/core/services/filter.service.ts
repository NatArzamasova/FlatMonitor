import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class FilterService {
  filterByDistricts<T>(
    data: T[],
    districts: string[],
    selector: (item: T) => string | undefined
  ): T[] {
    if (districts.length === 0) {
      return data;
    }
    return data.filter(item => districts.includes(selector(item) || ''));
  }

  filterByRooms<T>(
    data: T[],
    rooms: number[],
    selector: (item: T) => number | undefined | null
  ): T[] {
    if (rooms.length === 0) {
      return data;
    }
    return data.filter(item => {
      const value = selector(item);
      return value !== undefined && value !== null && rooms.includes(value);
    });
  }

  filterByPriceRange<T>(
    data: T[],
    min: number | null,
    max: number | null,
    selector: (item: T) => number | undefined
  ): T[] {
    const lower = min !== null && min > 0 ? min : null;
    const upper = max !== null && max > 0 ? max : null;
    if (lower === null && upper === null) {
      return data;
    }

    return data.filter(item => {
      const value = selector(item);
      if (value === undefined) {
        return false;
      }
      if (lower !== null && value < lower) {
        return false;
      }
      if (upper !== null && value > upper) {
        return false;
      }
      return true;
    });
  }
}
