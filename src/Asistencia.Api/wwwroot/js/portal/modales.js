// Modales nativos <dialog class="modal" id="...">
//   Abrir:  <button data-abrir-modal="idDelModal">
//   Cerrar: <button data-cerrar-modal>, clic fuera del cuadro o tecla Esc
//   Rellenar al abrir: data-rellenar='{"estudianteId":5,"nombre":"Ana"}'
//     llena <input name="estudianteId"> y <span data-campo="nombre"> del modal
//   Abrir al cargar la página: <dialog data-abrir-al-cargar>

export function abrirModal(id) {
    const dlg = document.getElementById(id);
    if (!dlg) return null;
    dlg.showModal();
    const foco = dlg.querySelector('[autofocus], input:not([type=hidden]), select, textarea');
    foco?.focus();
    return dlg;
}

export function iniciarModales() {
    document.addEventListener('click', e => {
        const abrir = e.target.closest('[data-abrir-modal]');
        if (abrir) { e.preventDefault(); abrirModal(abrir.dataset.abrirModal); return; }

        const cerrar = e.target.closest('[data-cerrar-modal]');
        if (cerrar) { e.preventDefault(); cerrar.closest('dialog')?.close(); return; }

        // Clic en el fondo oscuro (fuera del contenido del <dialog>)
        if (e.target instanceof HTMLDialogElement && e.target.classList.contains('modal')) {
            const r = e.target.getBoundingClientRect();
            const dentro = e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom;
            if (!dentro) e.target.close();
        }
    });

    // Modal que debe abrirse al cargar (por ejemplo, tras un error de validación)
    document.querySelectorAll('dialog[data-abrir-al-cargar]').forEach(d => d.showModal());

    // Rellenar los campos del modal con los datos del botón que lo abre (fase de captura,
    // para que ocurra antes de abrirlo).
    document.addEventListener('click', e => {
        const btn = e.target.closest('[data-rellenar]');
        if (!btn) return;
        const dlg = document.getElementById(btn.dataset.abrirModal);
        if (!dlg) return;
        const datos = JSON.parse(btn.dataset.rellenar);
        for (const [clave, valor] of Object.entries(datos)) {
            dlg.querySelectorAll(`[name="${clave}"]`).forEach(el => {
                if (el.type === 'radio') el.checked = el.value === String(valor ?? '');
                else el.value = valor ?? '';
            });
            dlg.querySelectorAll(`[data-campo="${clave}"]`).forEach(el => el.textContent = valor ?? '');
        }
    }, true);
}
