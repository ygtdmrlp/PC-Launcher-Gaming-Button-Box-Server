// PC Controller & Button Box Deck - Modern Web UI Logic
document.addEventListener('DOMContentLoaded', () => {
    // State
    let currentMode = 'keys'; // 'keys' or 'apps'
    let keys = [];
    let apps = [];
    let currentCategory = 'ALL';
    let searchQuery = '';
    let isOnline = false;
    let requiresPin = false;
    let storedPin = sessionStorage.getItem('pclauncher_pin') || '';

    // DOM Elements
    const headerTitle = document.getElementById('headerTitle');
    const headerSubtitle = document.getElementById('headerSubtitle');

    const modeKeysBtn = document.getElementById('modeKeysBtn');
    const modeAppsBtn = document.getElementById('modeAppsBtn');
    const deckReloadBtn = document.getElementById('deckReloadBtn');
    const fullscreenBtn = document.getElementById('fullscreenBtn');
    const fsEnterIcon = document.getElementById('fsEnterIcon');
    const fsExitIcon = document.getElementById('fsExitIcon');

    const categoryChips = document.getElementById('categoryChips');
    const searchWrap = document.getElementById('searchWrap');
    const searchInput = document.getElementById('searchInput');
    const clearSearchBtn = document.getElementById('clearSearchBtn');

    const loadingView = document.getElementById('loadingView');
    const emptyKeysView = document.getElementById('emptyKeysView');
    const emptyAppsView = document.getElementById('emptyAppsView');
    const noResultsView = document.getElementById('noResultsView');

    const keysDeckGrid = document.getElementById('keysDeckGrid');
    const appsDeckGrid = document.getElementById('appsDeckGrid');

    // PIN Elements
    const pinModal = document.getElementById('pinModal');
    const pinInput = document.getElementById('pinInput');
    const pinError = document.getElementById('pinError');
    const pinSubmitBtn = document.getElementById('pinSubmitBtn');

    // Toast
    const toast = document.getElementById('toast');

    // Initialize
    init();

    async function init() {
        setupListeners();
        setupFullscreen();
        await checkStatus();
        await loadData();

        // Status check heartbeat every 4 seconds
        setInterval(checkStatus, 4000);
    }

    function setupListeners() {
        // Mode Switcher
        modeKeysBtn.addEventListener('click', () => switchMode('keys'));
        modeAppsBtn.addEventListener('click', () => switchMode('apps'));

        // Reload
        deckReloadBtn.addEventListener('click', async () => {
            deckReloadBtn.style.transform = 'rotate(180deg)';
            setTimeout(() => deckReloadBtn.style.transform = '', 300);
            await checkStatus();
            await loadData();
            showToast('Yenilendi');
        });

        // Search
        searchInput.addEventListener('input', (e) => {
            searchQuery = e.target.value.trim().toLowerCase();
            clearSearchBtn.style.display = searchQuery ? 'block' : 'none';
            renderCurrentView();
        });

        clearSearchBtn.addEventListener('click', () => {
            searchInput.value = '';
            searchQuery = '';
            clearSearchBtn.style.display = 'none';
            searchInput.focus();
            renderCurrentView();
        });

        // Category Chips Click
        categoryChips.addEventListener('click', (e) => {
            const chip = e.target.closest('.chip');
            if (!chip) return;

            document.querySelectorAll('.chip').forEach(c => c.classList.remove('active'));
            chip.classList.add('active');

            currentCategory = chip.getAttribute('data-cat');
            renderCurrentView();
        });

        // PIN Form
        pinSubmitBtn.addEventListener('click', submitPin);
        pinInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') submitPin();
        });
    }

    // Fullscreen API implementation
    function setupFullscreen() {
        fullscreenBtn.addEventListener('click', toggleFullscreen);

        // Listen to change events
        document.addEventListener('fullscreenchange', handleFullscreenChange);
        document.addEventListener('webkitfullscreenchange', handleFullscreenChange);
        document.addEventListener('mozfullscreenchange', handleFullscreenChange);
        document.addEventListener('MSFullscreenChange', handleFullscreenChange);
    }

    function isFullscreenActive() {
        return !!(
            document.fullscreenElement ||
            document.webkitFullscreenElement ||
            document.mozFullScreenElement ||
            document.msFullscreenElement
        );
    }

    function toggleFullscreen() {
        vibrate(30);

        if (!isFullscreenActive()) {
            const elem = document.documentElement;
            if (elem.requestFullscreen) {
                elem.requestFullscreen().catch(err => {
                    console.warn('Fullscreen request failed:', err);
                });
            } else if (elem.webkitRequestFullscreen) {
                elem.webkitRequestFullscreen();
            } else if (elem.mozRequestFullScreen) {
                elem.mozRequestFullScreen();
            } else if (elem.msRequestFullscreen) {
                elem.msRequestFullscreen();
            }
            showToast('Tam Ekran Modu');
        } else {
            if (document.exitFullscreen) {
                document.exitFullscreen().catch(() => {});
            } else if (document.webkitExitFullscreen) {
                document.webkitExitFullscreen();
            } else if (document.mozCancelFullScreen) {
                document.mozCancelFullScreen();
            } else if (document.msExitFullscreen) {
                document.msExitFullscreen();
            }
            showToast('Pencere Modu');
        }
    }

    function handleFullscreenChange() {
        const isFs = isFullscreenActive();
        fullscreenBtn.classList.toggle('active', isFs);

        if (isFs) {
            fsEnterIcon.style.display = 'none';
            fsExitIcon.style.display = 'block';
            fullscreenBtn.title = "Tam Ekrandan Çık";
        } else {
            fsEnterIcon.style.display = 'block';
            fsExitIcon.style.display = 'none';
            fullscreenBtn.title = "Tam Ekran Yap";
        }
    }

    function switchMode(mode) {
        currentMode = mode;
        searchQuery = '';
        searchInput.value = '';
        currentCategory = 'ALL';

        if (mode === 'keys') {
            modeKeysBtn.classList.add('active');
            modeAppsBtn.classList.remove('active');
            headerTitle.textContent = "Tuş Takımı";
            headerSubtitle.textContent = "Kısayol tuşları ile hızlı erişim";
            keysDeckGrid.style.display = 'grid';
            appsDeckGrid.style.display = 'none';
            searchWrap.style.display = 'none';
        } else {
            modeAppsBtn.classList.add('active');
            modeKeysBtn.classList.remove('active');
            headerTitle.textContent = "Programlar";
            headerSubtitle.textContent = "Bilgisayardaki uygulamaları uzaktan başlatın";
            keysDeckGrid.style.display = 'none';
            appsDeckGrid.style.display = 'grid';
            searchWrap.style.display = 'flex';
        }

        updateCategoryChips();
        renderCurrentView();
    }

    async function checkStatus() {
        try {
            const res = await fetch('/api/status', { cache: 'no-store' });
            if (!res.ok) throw new Error('Status failed');

            const data = await res.json();
            isOnline = true;
            requiresPin = !!data.requiresPin;

            if (requiresPin && !storedPin) {
                showPinModal();
            }
        } catch {
            isOnline = false;
        }
    }

    async function loadData() {
        loadingView.style.display = 'flex';
        emptyKeysView.style.display = 'none';
        emptyAppsView.style.display = 'none';
        noResultsView.style.display = 'none';

        try {
            const [keysRes, appsRes] = await Promise.all([
                fetch('/api/keys', { headers: getAuthHeaders(), cache: 'no-store' }),
                fetch('/api/apps', { headers: getAuthHeaders(), cache: 'no-store' })
            ]);

            if (keysRes.status === 401 || appsRes.status === 401) {
                showPinModal();
                loadingView.style.display = 'none';
                return;
            }

            if (keysRes.ok) keys = await keysRes.json();
            if (appsRes.ok) apps = await appsRes.json();

            loadingView.style.display = 'none';

            updateCategoryChips();
            renderCurrentView();
        } catch (err) {
            loadingView.style.display = 'none';
            renderCurrentView();
        }
    }

    function updateCategoryChips() {
        const dataset = currentMode === 'keys' ? keys : apps;
        const categories = new Set();

        dataset.forEach(item => {
            const cat = currentMode === 'keys' ? item.gameOrCategory : item.category;
            if (cat && cat.trim()) categories.add(cat.trim());
        });

        let html = `<button class="chip ${currentCategory === 'ALL' ? 'active' : ''}" data-cat="ALL">Tümü</button>`;
        categories.forEach(cat => {
            const active = currentCategory === cat ? 'active' : '';
            html += `<button class="chip ${active}" data-cat="${escapeHtml(cat)}">${escapeHtml(cat)}</button>`;
        });

        categoryChips.innerHTML = html;
    }

    function renderCurrentView() {
        loadingView.style.display = 'none';

        if (currentMode === 'keys') {
            renderKeysDeck();
        } else {
            renderAppsDeck();
        }
    }

    // Render Gaming Button Box Grid (Matching uploaded screenshot)
    function renderKeysDeck() {
        appsDeckGrid.style.display = 'none';
        emptyAppsView.style.display = 'none';

        if (keys.length === 0) {
            emptyKeysView.style.display = 'flex';
            keysDeckGrid.style.display = 'none';
            noResultsView.style.display = 'none';
            return;
        }

        let filtered = keys;
        if (currentCategory !== 'ALL') {
            filtered = filtered.filter(k => (k.gameOrCategory || '').toLowerCase() === currentCategory.toLowerCase());
        }

        filtered.sort((a, b) => (a.order - b.order) || a.title.localeCompare(b.title));

        if (filtered.length === 0) {
            noResultsView.style.display = 'flex';
            keysDeckGrid.style.display = 'none';
            emptyKeysView.style.display = 'none';
            return;
        }

        noResultsView.style.display = 'none';
        emptyKeysView.style.display = 'none';
        keysDeckGrid.style.display = 'grid';

        keysDeckGrid.innerHTML = filtered.map(keyItem => {
            const color = keyItem.colorHex || '#3b82f6';
            const iconHtml = keyItem.iconBase64
                ? `<img src="${keyItem.iconBase64}" class="deck-btn-image-icon" alt="">`
                : `<span class="deck-btn-preset-icon">${keyItem.presetIcon || '🎮'}</span>`;

            return `
                <div class="deck-button" data-id="${keyItem.id}" style="--button-color: ${color};">
                    <div class="deck-btn-icon-wrapper">
                        ${iconHtml}
                    </div>
                    <div class="deck-btn-title">${escapeHtml(keyItem.title)}</div>
                    <div class="deck-btn-bottom-row">
                        <div class="deck-btn-key-badge">${escapeHtml(keyItem.keySequence)}</div>
                        <div class="deck-btn-chevron">
                            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                                <polyline points="9 18 15 12 9 6"></polyline>
                            </svg>
                        </div>
                    </div>
                </div>
            `;
        }).join('');

        // Attach touch & click handlers
        keysDeckGrid.querySelectorAll('.deck-button').forEach(btn => {
            const keyId = btn.getAttribute('data-id');

            // Visual active feedback
            const pressStart = () => {
                btn.classList.add('pressed');
                vibrate(40);
            };

            const pressEnd = () => {
                setTimeout(() => btn.classList.remove('pressed'), 120);
            };

            btn.addEventListener('mousedown', pressStart);
            btn.addEventListener('mouseup', pressEnd);
            btn.addEventListener('mouseleave', pressEnd);

            btn.addEventListener('touchstart', pressStart, { passive: true });
            btn.addEventListener('touchend', pressEnd, { passive: true });

            btn.addEventListener('click', () => {
                triggerKeyPress(keyId, btn);
            });
        });
    }

    // Trigger Key Press via API
    async function triggerKeyPress(keyId, btnElement) {
        const item = keys.find(k => k.id === keyId);
        const title = item ? item.title : 'Tuş';

        try {
            const res = await fetch(`/api/keys/${keyId}/press`, {
                method: 'POST',
                headers: getAuthHeaders()
            });

            if (res.status === 401) {
                showPinModal();
                return;
            }

            const data = await res.json();
            if (data.success) {
                showToast(`Basıldı: ${title}`);
            } else {
                showToast(`İletilemedi: ${data.message || 'Bilinmeyen hata'}`);
            }
        } catch (err) {
            showToast('PC bağlantısı kesildi!');
        }
    }

    // Render Apps Deck Grid
    function renderAppsDeck() {
        keysDeckGrid.style.display = 'none';
        emptyKeysView.style.display = 'none';

        if (apps.length === 0) {
            emptyAppsView.style.display = 'flex';
            appsDeckGrid.style.display = 'none';
            noResultsView.style.display = 'none';
            return;
        }

        let filtered = apps;
        if (currentCategory !== 'ALL') {
            filtered = filtered.filter(a => (a.category || '').toLowerCase() === currentCategory.toLowerCase());
        }

        if (searchQuery) {
            filtered = filtered.filter(a =>
                (a.name || '').toLowerCase().includes(searchQuery) ||
                (a.category || '').toLowerCase().includes(searchQuery)
            );
        }

        filtered.sort((a, b) => (a.order - b.order) || a.name.localeCompare(b.name));

        if (filtered.length === 0) {
            noResultsView.style.display = 'flex';
            appsDeckGrid.style.display = 'none';
            emptyAppsView.style.display = 'none';
            return;
        }

        noResultsView.style.display = 'none';
        emptyAppsView.style.display = 'none';
        appsDeckGrid.style.display = 'grid';

        appsDeckGrid.innerHTML = filtered.map(app => {
            return `
                <div class="app-deck-card" data-id="${app.id}">
                    <div class="app-icon-wrap">
                        <img src="/api/apps/${app.id}/icon" class="app-icon-img" alt="${escapeHtml(app.name)}" onerror="this.src='/favicon.ico'">
                    </div>
                    <div class="app-card-title">${escapeHtml(app.name)}</div>
                    <div class="app-card-bottom">
                        <div class="app-launch-badge">Başlat</div>
                        <div class="deck-btn-chevron">
                            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                                <polyline points="9 18 15 12 9 6"></polyline>
                            </svg>
                        </div>
                    </div>
                </div>
            `;
        }).join('');

        appsDeckGrid.querySelectorAll('.app-deck-card').forEach(card => {
            const appId = card.getAttribute('data-id');

            const pressStart = () => {
                card.style.transform = 'scale(0.95)';
                vibrate(40);
            };

            const pressEnd = () => {
                setTimeout(() => card.style.transform = '', 120);
            };

            card.addEventListener('mousedown', pressStart);
            card.addEventListener('mouseup', pressEnd);
            card.addEventListener('mouseleave', pressEnd);

            card.addEventListener('touchstart', pressStart, { passive: true });
            card.addEventListener('touchend', pressEnd, { passive: true });

            card.addEventListener('click', () => {
                triggerAppLaunch(appId);
            });
        });
    }

    async function triggerAppLaunch(appId) {
        const app = apps.find(a => a.id === appId);
        const name = app ? app.name : 'Uygulama';

        try {
            const res = await fetch(`/api/apps/${appId}/launch`, {
                method: 'POST',
                headers: getAuthHeaders()
            });

            if (res.status === 401) {
                showPinModal();
                return;
            }

            const data = await res.json();
            if (data.success) {
                showToast(`Açıldı: ${name}`);
            } else {
                showToast(`Başlatılamadı: ${data.message || 'Hata'}`);
            }
        } catch {
            showToast('PC bağlantısı kesildi!');
        }
    }

    // PIN Authentication
    function showPinModal() {
        pinModal.style.display = 'flex';
        pinInput.value = '';
        pinError.style.display = 'none';
        setTimeout(() => pinInput.focus(), 100);
    }

    async function submitPin() {
        const pin = pinInput.value.trim();
        if (!pin) return;

        try {
            const res = await fetch('/api/auth/verify', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ pin })
            });

            if (res.ok) {
                storedPin = pin;
                sessionStorage.setItem('pclauncher_pin', pin);
                pinModal.style.display = 'none';
                await loadData();
            } else {
                pinError.style.display = 'block';
                pinInput.value = '';
                vibrate([50, 50, 50]);
            }
        } catch {
            pinError.textContent = "Bağlantı hatası.";
            pinError.style.display = 'block';
        }
    }

    function getAuthHeaders() {
        const headers = {};
        if (storedPin) {
            headers['X-Launcher-PIN'] = storedPin;
        }
        return headers;
    }

    function showToast(text) {
        toast.textContent = text;
        toast.classList.add('show');
        clearTimeout(toast._timer);
        toast._timer = setTimeout(() => {
            toast.classList.remove('show');
        }, 1800);
    }

    function vibrate(ms) {
        if (navigator.vibrate) {
            try { navigator.vibrate(ms); } catch { }
        }
    }

    function escapeHtml(str) {
        if (!str) return '';
        return str
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }
});
