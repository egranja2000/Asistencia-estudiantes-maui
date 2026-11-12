// Comportamientos de formularios del portal.

// Formulario "Nueva sesión" (<form data-form-sesion>): muestra los campos según la
// modalidad (.solo-online / .solo-presencial) y sugiere la duración del checkpoint.
function iniciarFormularioSesion() {
    document.querySelectorAll('[data-form-sesion]').forEach(form => {
        const radios = form.querySelectorAll('input[name="Modalidad"]');
        const duracion = form.querySelector('[data-duracion]');
        const esOnline = () => form.querySelector('input[name="Modalidad"]:checked')?.value === 'Online';
        const aplicar = () => {
            const online = esOnline();
            form.querySelectorAll('.solo-online').forEach(e => e.hidden = !online);
            form.querySelectorAll('.solo-presencial').forEach(e => e.hidden = online);
        };
        radios.forEach(r => r.addEventListener('change', () => { aplicar(); if (duracion) duracion.value = esOnline() ? 60 : 300; }));
        aplicar();
    });
}

// Botones de valor rápido: <button data-poner-valor="idDelInput" data-valor="60">
// escriben ese valor en el input indicado.
function iniciarValoresRapidos() {
    document.addEventListener('click', e => {
        const btn = e.target.closest('[data-poner-valor]');
        if (!btn) return;
        const destino = document.getElementById(btn.dataset.ponerValor);
        if (destino) {
            destino.value = btn.dataset.valor;
            destino.dispatchEvent(new Event('change', { bubbles: true }));
        }
    });
}

// Filas clicables en tablas: <tr data-href="/ruta">
function iniciarFilasClicables() {
    document.addEventListener('click', e => {
        const tr = e.target.closest('tr[data-href]');
        if (tr && !e.target.closest('a, button, input, select, form')) location.href = tr.dataset.href;
    });
}

export function iniciarFormularios() {
    iniciarFormularioSesion();
    iniciarValoresRapidos();
    iniciarFilasClicables();
}
