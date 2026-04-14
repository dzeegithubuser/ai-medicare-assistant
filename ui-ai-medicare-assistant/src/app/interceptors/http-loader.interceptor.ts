import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize } from 'rxjs/operators';
import { HttpLoaderService } from '../services/http-loader.service';

const EXCLUDED_URL_FRAGMENT = '/api/chat/session/messages';

export const httpLoaderInterceptor: HttpInterceptorFn = (req, next) => {
  const loader = inject(HttpLoaderService);
  if (req.url.includes(EXCLUDED_URL_FRAGMENT)) {
    return next(req);
  }

  loader.begin();
  return next(req).pipe(
    finalize(() => loader.end())
  );
};
