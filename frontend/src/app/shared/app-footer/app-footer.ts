import { Component } from '@angular/core';

@Component({
  selector: 'app-footer',
  template: `<footer class="brand-footer">
    <div><strong>EIP</strong><span>Inteligência corporativa</span></div>
    <p>© {{ year }} JP Code Craft Solutions. Todos os direitos reservados.</p>
    <p>
      Desenvolvido por
      <a href="https://www.jpcodecraft.com.br/" target="_blank" rel="noopener noreferrer"
        >JP Code Craft Solutions <span aria-hidden="true">↗</span></a
      >
    </p>
  </footer>`,
  styles: `
    :host {
      display: block;
    }
    .brand-footer {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 12px 24px;
      padding: 22px 32px;
      border-top: 1px solid var(--jp-border);
      background: var(--jp-paper);
      color: var(--jp-muted);
      font-size: 12px;
      line-height: 1.6;
    }
    .brand-footer div {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    strong {
      color: var(--jp-ink);
      letter-spacing: 0.08em;
    }
    p {
      margin: 0;
    }
    a {
      color: var(--jp-green);
      font-weight: 600;
      text-decoration: underline;
      text-underline-offset: 3px;
    }
    @media (max-width: 640px) {
      .brand-footer {
        padding: 20px 24px;
        flex-direction: column;
        align-items: flex-start;
        gap: 8px;
      }
    }
  `,
})
export class AppFooter {
  readonly year = new Date().getFullYear();
}
