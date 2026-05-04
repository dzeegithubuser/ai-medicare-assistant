import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { LabelValuePair } from '../models/profile.model';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';

export interface CountyCodeEntry {
  city: string;
  countyName: string;
  countyCode: string;
  state: string;
  latitude: number;
  longitude: number;
}

@Injectable({ providedIn: 'root' })
export class CountyLookupService {
  private cache = new Map<string, CountyCodeEntry[]>();
  private magiCache = new Map<string, LabelValuePair[]>();

  constructor(private http: HttpClient) {}

  getCountyCodeList(zipcode: string): Observable<CountyCodeEntry[]> {
    const zip = zipcode?.trim();
    if (!zip || zip.length < 5) return of([]);

    if (this.cache.has(zip)) {
      return of(this.cache.get(zip)!);
    }

    return this.http.post<CountyCodeEntry[]>(
      `${environment.apiUrl}/api/CountyLookup/getCountycodeList`,
      { zipcode: zip }
    ).pipe(
      map(results => {
        this.cache.set(zip, results);
        return results;
      }),
      catchError(() => of([]))
    );
  }

  getMagiTiers(filingStatus: string, coverageYear: number): Observable<LabelValuePair[]> {
    if (!filingStatus || !coverageYear) return of([]);
    const cacheKey = `${filingStatus}|${coverageYear}`;
    if (this.magiCache.has(cacheKey)) {
      return of(this.magiCache.get(cacheKey)!);
    }
    return this.http.get<LabelValuePair[]>(
      `${environment.apiUrl}/api/CountyLookup/constants/magi-tiers`,
      { params: { filingStatus, coverageYear: coverageYear.toString() } }
    ).pipe(
      map(results => {
        this.magiCache.set(cacheKey, results);
        return results;
      }),
      catchError(() => of([]))
    );
  }
}
