// Relógio na topbar
function atualizarRelogio() {
    var el = document.getElementById('topbar-date');
    if (!el) return;
    var now = new Date();
    el.textContent = now.toLocaleDateString('pt-BR', {
        weekday: 'short',
        day: '2-digit',
        month: 'short',
        year: 'numeric'
    });
}
atualizarRelogio();
setInterval(atualizarRelogio, 60000);