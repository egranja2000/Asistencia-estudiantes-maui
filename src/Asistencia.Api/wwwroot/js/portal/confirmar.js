// Confirmación antes de enviar un formulario, usando el modal #modalConfirmar del layout
// en lugar de window.confirm:
//   <form data-confirmar="¿Seguro?" data-confirmar-titulo="..." data-confirmar-boton="Sí, cerrar">

export function iniciarConfirmaciones() {
    document.addEventListener('submit', e => {
        const form = e.target;
        if (!form.dataset.confirmar || form.dataset.confirmado === '1') return;
        e.preventDefault();

        const dlg = document.getElementById('modalConfirmar');
        dlg.querySelector('[data-confirmar-titulo]').textContent = form.dataset.confirmarTitulo || 'Confirmar';
        dlg.querySelector('[data-confirmar-mensaje]').textContent = form.dataset.confirmar;
        const ok = dlg.querySelector('[data-confirmar-ok]');
        ok.textContent = form.dataset.confirmarBoton || 'Confirmar';
        ok.onclick = () => { form.dataset.confirmado = '1'; dlg.close(); form.requestSubmit(); };
        dlg.showModal();
    });
}
