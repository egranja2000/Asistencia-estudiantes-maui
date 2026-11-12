// Pantalla de proyección del checkpoint.
// Consulta el estado cada 2 s; solo cambia la imagen QR cuando cambia el "paso" (cada RotacionSegundos).
(function () {
    const app = document.getElementById('app');
    const url = app.dataset.url;
    const $ = id => document.getElementById(id);

    let pasoActual = null;
    let segToken = 0, rotacion = 20, segCheckpoint = 0;

    function formatoCodigo(c) { return c ? c.slice(0, 3) + ' ' + c.slice(3) : '––– –––'; }
    function mmss(s) { const m = Math.floor(s / 60); return m + ':' + String(s % 60).padStart(2, '0'); }

    function mostrar(seccion) {
        ['activo', 'espera', 'cerrada'].forEach(id => $(id).hidden = id !== seccion);
    }

    function pintarContadores() {
        $('segToken').textContent = Math.max(0, segToken);
        $('segCheckpoint').textContent = mmss(Math.max(0, segCheckpoint));
        $('barraToken').style.width = (100 * Math.max(0, segToken) / rotacion) + '%';
    }

    async function consultar() {
        try {
            const r = await fetch(url, { cache: 'no-store', credentials: 'same-origin' });
            if (r.status === 401 || r.redirected) { location.reload(); return; }
            if (!r.ok) throw new Error(r.status);
            const e = await r.json();
            $('estadoRed').textContent = '';
            $('estadoRed').classList.remove('error');

            $('registrados').textContent = e.registrados;
            $('matriculados').textContent = e.matriculados;

            if (e.estadoSesion === 'Cerrada') { mostrar('cerrada'); return; }
            if (!e.activo) { mostrar('espera'); pasoActual = null; return; }

            mostrar('activo');
            if (e.paso !== pasoActual) {
                pasoActual = e.paso;
                $('qr').src = e.qr;
                $('codigo').textContent = formatoCodigo(e.codigo);
                $('orden').textContent = e.orden;
            }
            segToken = e.segundosToken;
            rotacion = e.rotacionSegundos || 20;
            segCheckpoint = e.segundosCheckpoint;
            pintarContadores();
        } catch (err) {
            $('estadoRed').textContent = 'Sin conexión con el servidor… reintentando';
            $('estadoRed').classList.add('error');
        }
    }

    // Cuenta regresiva local entre consultas
    setInterval(() => { segToken--; segCheckpoint--; pintarContadores(); }, 1000);
    setInterval(consultar, 2000);
    consultar();

    $('pantallaCompleta').addEventListener('click', () => {
        if (!document.fullscreenElement) document.documentElement.requestFullscreen?.();
        else document.exitFullscreen?.();
    });
})();
