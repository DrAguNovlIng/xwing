window.xwingTheme = {
    setPreference: function (value, maxAgeSeconds) {
        const encoded = encodeURIComponent(value);
        document.cookie = 'xwing-theme=' + encoded + '; path=/; max-age=' + maxAgeSeconds + '; SameSite=Lax';
    },
    capturePointer: function (element, pointerId) {
        if (element && typeof element.setPointerCapture === 'function') {
            element.setPointerCapture(pointerId);
        }
    },
    attachMatchDrag: function (surfaceElement, dotnetRef, options) {
        if (!surfaceElement || !dotnetRef) {
            return;
        }

        window.xwingTheme.disposeMatchDrag(surfaceElement);

        const cellSize = options.cellSize ?? 40;
        const boxOffset = options.boxOffset ?? 6;
        const maxX = options.maxX ?? 0;
        const maxY = options.maxY ?? 0;
        const handleSelector = '.template-match-drag-handle';
        const boxSelector = '.template-match-box[data-match-id]';

        let dragState = null;

        const snapCell = (clientX, clientY) => {
            const rect = surfaceElement.getBoundingClientRect();
            const offsetX = clientX - rect.left;
            const offsetY = clientY - rect.top;
            let x = Math.floor(offsetX / cellSize);
            let y = Math.floor(offsetY / cellSize);
            x = Math.max(0, Math.min(x, maxX));
            y = Math.max(0, Math.min(y, maxY));
            return { x: x, y: y };
        };

        const applyBoxPosition = (matchId, cellX, cellY) => {
            const box = surfaceElement.querySelector('.template-match-box[data-match-id="' + matchId + '"]');
            if (!box) {
                return;
            }

            const left = cellX * cellSize + boxOffset;
            const top = cellY * cellSize + boxOffset;
            box.style.left = left + 'px';
            box.style.top = top + 'px';
        };

        const onPointerDown = (e) => {
            const handle = e.target.closest(handleSelector);
            if (!handle || !surfaceElement.contains(handle)) {
                return;
            }

            const box = handle.closest(boxSelector);
            if (!box) {
                return;
            }

            const matchId = parseInt(box.getAttribute('data-match-id'), 10);
            if (Number.isNaN(matchId)) {
                return;
            }

            e.preventDefault();
            e.stopPropagation();
            if (typeof surfaceElement.setPointerCapture === 'function') {
                surfaceElement.setPointerCapture(e.pointerId);
            }

            const startCell = snapCell(e.clientX, e.clientY);
            dragState = {
                pointerId: e.pointerId,
                matchId: matchId,
                cellX: startCell.x,
                cellY: startCell.y,
            };
            surfaceElement.classList.add('template-graph-surface-dragging');
            box.classList.add('dragging');
            applyBoxPosition(matchId, startCell.x, startCell.y);
            dotnetRef.invokeMethodAsync('DragStarted', matchId, startCell.x, startCell.y);
        };

        const onPointerMove = (e) => {
            if (!dragState || dragState.pointerId !== e.pointerId) {
                return;
            }

            e.preventDefault();
            const cell = snapCell(e.clientX, e.clientY);
            if (cell.x === dragState.cellX && cell.y === dragState.cellY) {
                return;
            }

            dragState.cellX = cell.x;
            dragState.cellY = cell.y;
            applyBoxPosition(dragState.matchId, cell.x, cell.y);
            dotnetRef.invokeMethodAsync('DragMoved', dragState.matchId, cell.x, cell.y);
        };

        const endDrag = (e) => {
            if (!dragState || dragState.pointerId !== e.pointerId) {
                return;
            }

            e.preventDefault();
            const matchId = dragState.matchId;
            const cellX = dragState.cellX;
            const cellY = dragState.cellY;
            if (typeof surfaceElement.releasePointerCapture === 'function') {
                try {
                    surfaceElement.releasePointerCapture(e.pointerId);
                } catch (_) {
                    // Pointer may already be released.
                }
            }

            surfaceElement.classList.remove('template-graph-surface-dragging');
            const box = surfaceElement.querySelector('.template-match-box[data-match-id="' + matchId + '"]');
            if (box) {
                box.classList.remove('dragging');
            }

            dragState = null;
            dotnetRef.invokeMethodAsync('DragEnded', matchId, cellX, cellY);
        };

        surfaceElement.addEventListener('pointerdown', onPointerDown);
        surfaceElement.addEventListener('pointermove', onPointerMove);
        surfaceElement.addEventListener('pointerup', endDrag);
        surfaceElement.addEventListener('pointercancel', endDrag);

        surfaceElement.__xwingMatchDrag = {
            dispose: function () {
                surfaceElement.removeEventListener('pointerdown', onPointerDown);
                surfaceElement.removeEventListener('pointermove', onPointerMove);
                surfaceElement.removeEventListener('pointerup', endDrag);
                surfaceElement.removeEventListener('pointercancel', endDrag);
                dragState = null;
            },
        };
    },
    disposeMatchDrag: function (surfaceElement) {
        if (surfaceElement && surfaceElement.__xwingMatchDrag) {
            surfaceElement.__xwingMatchDrag.dispose();
            delete surfaceElement.__xwingMatchDrag;
        }
    },
};
