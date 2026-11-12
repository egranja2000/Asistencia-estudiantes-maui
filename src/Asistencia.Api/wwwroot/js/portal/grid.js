// Grid de datos: búsqueda sin tildes, orden por columna, paginación y exportar a CSV (Excel).
//   <div data-grid data-grid-tamano="10" data-grid-archivo="sesiones">
//     <input data-grid-buscar>  <button data-grid-exportar>
//     <table> <th data-ordenable data-tipo="texto|numero"> ... <td data-valor="valor para ordenar">
//     <th data-no-exportar>, <td data-exportar="texto para el CSV">
//     <div data-grid-vacio hidden>   <div data-grid-pie>

const comparador = new Intl.Collator('es', { numeric: true, sensitivity: 'base' });

function valorCelda(tr, i) {
    const td = tr.cells[i];
    if (!td) return '';
    return td.dataset.valor ?? td.textContent.trim();
}

function iniciarGrid(raiz) {
    const tabla = raiz.querySelector('table');
    const cuerpo = tabla.tBodies[0];
    const filas = Array.from(cuerpo.rows).filter(r => !r.hasAttribute('data-sin-datos'));
    const buscar = raiz.querySelector('[data-grid-buscar]');
    const vacio = raiz.querySelector('[data-grid-vacio]');
    const pie = raiz.querySelector('[data-grid-pie]');
    let tamano = parseInt(raiz.dataset.gridTamano || '10', 10);
    let pagina = 1, filtradas = filas, orden = { col: -1, dir: 1 };

    // Texto de búsqueda precalculado por fila
    filas.forEach(f => f._texto = f.textContent.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, ''));

    // Encabezados ordenables
    Array.from(tabla.tHead.rows[0].cells).forEach((th, i) => {
        if (!th.hasAttribute('data-ordenable')) return;
        th.insertAdjacentHTML('beforeend', '<span class="flecha" aria-hidden="true">↕</span>');
        th.tabIndex = 0;
        const ordenar = () => {
            orden = { col: i, dir: orden.col === i ? -orden.dir : 1 };
            tabla.tHead.querySelectorAll('th').forEach(h => { h.removeAttribute('aria-sort'); const f = h.querySelector('.flecha'); if (f) f.textContent = '↕'; });
            th.setAttribute('aria-sort', orden.dir === 1 ? 'ascending' : 'descending');
            th.querySelector('.flecha').textContent = orden.dir === 1 ? '↑' : '↓';
            const numerico = th.dataset.tipo === 'numero';
            filas.sort((a, b) => {
                const va = valorCelda(a, i), vb = valorCelda(b, i);
                const r = numerico ? (parseFloat(va) || -Infinity) - (parseFloat(vb) || -Infinity) : comparador.compare(va, vb);
                return r * orden.dir;
            });
            filas.forEach(f => cuerpo.appendChild(f));
            aplicar();
        };
        th.addEventListener('click', ordenar);
        th.addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); ordenar(); } });
    });

    function aplicar() {
        const q = (buscar?.value || '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').trim();
        filtradas = q ? filas.filter(f => f._texto.includes(q)) : filas.slice();
        const paginas = Math.max(1, Math.ceil(filtradas.length / tamano));
        pagina = Math.min(pagina, paginas);
        const desde = (pagina - 1) * tamano;
        const visibles = new Set(filtradas.slice(desde, desde + tamano));
        filas.forEach(f => f.hidden = !visibles.has(f));
        if (vacio) vacio.hidden = filtradas.length > 0;
        pintarPie(paginas, desde);
    }

    function pintarPie(paginas, desde) {
        if (!pie) return;
        const total = filtradas.length;
        const hasta = Math.min(desde + tamano, total);
        let botones = `<button type="button" class="grid-pagina" data-ir="${pagina - 1}" ${pagina === 1 ? 'disabled' : ''} aria-label="Anterior">‹</button>`;
        for (let p = 1; p <= paginas; p++) {
            if (paginas > 7 && p !== 1 && p !== paginas && Math.abs(p - pagina) > 1) {
                if (p === 2 || p === paginas - 1) botones += '<span class="px-1">…</span>';
                continue;
            }
            botones += `<button type="button" class="grid-pagina" data-ir="${p}" ${p === pagina ? 'aria-current="page"' : ''}>${p}</button>`;
        }
        botones += `<button type="button" class="grid-pagina" data-ir="${pagina + 1}" ${pagina === paginas ? 'disabled' : ''} aria-label="Siguiente">›</button>`;

        pie.innerHTML = `
            <div>${total === 0 ? 'Sin resultados' : `Mostrando <b class="text-slate-700">${desde + 1}–${hasta}</b> de <b class="text-slate-700">${total}</b>`}</div>
            <div class="flex items-center gap-3">
                <label class="flex items-center gap-2">Filas
                    <select class="rounded-md border-0 bg-white py-1 pl-2 pr-7 text-xs ring-1 ring-slate-300" data-tamano>
                        ${[5, 10, 25, 50, 100].map(n => `<option ${n === tamano ? 'selected' : ''}>${n}</option>`).join('')}
                    </select>
                </label>
                <nav class="flex items-center gap-1">${botones}</nav>
            </div>`;
    }

    pie?.addEventListener('click', e => {
        const b = e.target.closest('[data-ir]');
        if (b && !b.disabled) { pagina = parseInt(b.dataset.ir, 10); aplicar(); }
    });
    pie?.addEventListener('change', e => {
        if (e.target.matches('[data-tamano]')) { tamano = parseInt(e.target.value, 10); pagina = 1; aplicar(); }
    });
    buscar?.addEventListener('input', () => { pagina = 1; aplicar(); });

    raiz.querySelector('[data-grid-exportar]')?.addEventListener('click', () => exportarCsv(tabla, filtradas, raiz.dataset.gridArchivo || 'datos'));

    aplicar();
}

function exportarCsv(tabla, filas, nombre) {
    const ths = Array.from(tabla.tHead.rows[0].cells);
    const columnas = ths.map((th, i) => i).filter(i => !ths[i].hasAttribute('data-no-exportar'));
    const limpiar = s => '"' + String(s).replace(/\s+/g, ' ').trim().replace(/"/g, '""') + '"';
    const lineas = [columnas.map(i => limpiar(ths[i].textContent.replace(/[↕↑↓]/g, ''))).join(';')];
    filas.forEach(f => lineas.push(columnas.map(i => limpiar(f.cells[i]?.dataset.exportar ?? f.cells[i]?.textContent ?? '')).join(';')));
    // BOM + ";" para que Excel en español abra las columnas correctamente
    const blob = new Blob(['﻿' + lineas.join('\r\n')], { type: 'text/csv;charset=utf-8' });
    const a = Object.assign(document.createElement('a'), { href: URL.createObjectURL(blob), download: `${nombre}.csv` });
    document.body.appendChild(a); a.click(); a.remove();
    setTimeout(() => URL.revokeObjectURL(a.href), 1000);
}

export function iniciarGrids() {
    document.querySelectorAll('[data-grid]').forEach(iniciarGrid);
}
