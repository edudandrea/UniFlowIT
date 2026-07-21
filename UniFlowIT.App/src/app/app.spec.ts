import { TestBed } from '@angular/core/testing';
import { importProvidersFrom } from '@angular/core';
import { provideAnimations } from '@angular/platform-browser/animations';
import { ToastrModule } from 'ngx-toastr';
import { vi } from 'vitest';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    vi.spyOn(window, 'fetch').mockRejectedValue(new Error('API indisponivel no teste'));

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideAnimations(),
        importProvidersFrom(ToastrModule.forRoot()),
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render login page', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h2')?.textContent).toContain('Entrar no UniFlowIT');
  });
});
