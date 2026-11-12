// Avisos (TempData) que se muestran arriba a la derecha y se ocultan solos.
//   <div data-toast> ... <button data-toast-cerrar> </div>

const DURACION_MS = 6000;

export function iniciarAvisos() {
    document.querySelectorAll('[data-toast]').forEach(t => {
        const quitar = () => { t.style.transition = 'opacity .3s'; t.style.opacity = '0'; setTimeout(() => t.remove(), 300); };
        t.querySelector('[data-toast-cerrar]')?.addEventListener('click', quitar);
        setTimeout(quitar, DURACION_MS);
    });
}
