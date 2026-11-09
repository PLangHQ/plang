export class StreamHandler {
    constructor() {
        this.kind = "stream";
        this.activeStreams = new Map();
    }
    async handle(message, _ctx) {
        const { streamId, phase, bytes, contentType, fileName, actions, target } = message;
        switch (phase) {
            case 'start':
                this.handleStart(streamId, contentType, fileName, actions, target);
                break;
            case 'chunk':
                this.handleChunk(streamId, bytes);
                break;
            case 'end':
                this.handleEnd(streamId);
                break;
            case 'abort':
                this.handleAbort(streamId);
                break;
        }
    }
    handleStart(streamId, contentType, fileName, actions, target) {
        const isDownload = actions?.includes('download');
        const isStream = actions?.includes('stream');
        if (isDownload && fileName && contentType) {
            // File download
            this.activeStreams.set(streamId, {
                chunks: [],
                contentType: contentType,
                fileName: fileName
            });
        }
        else if (isStream && target && contentType) {
            // Media streaming to video/audio element
            this.activeStreams.set(streamId, {
                chunks: [],
                contentType: contentType,
                fileName: fileName || '',
                isMediaStream: true,
                target: target
            });
        }
    }
    handleChunk(streamId, bytes) {
        const stream = this.activeStreams.get(streamId);
        if (stream && bytes) {
            // bytes comes as base64 string from JSON
            const binaryData = this.base64ToUint8Array(bytes);
            stream.chunks.push(binaryData);
        }
    }
    handleEnd(streamId) {
        const stream = this.activeStreams.get(streamId);
        if (stream) {
            if (stream.isMediaStream && stream.target) {
                // Stream to media element
                this.streamToMediaElement(stream.chunks, stream.target, stream.contentType);
            }
            else {
                // Trigger download
                this.downloadFile(stream.chunks, stream.fileName, stream.contentType);
            }
            this.activeStreams.delete(streamId);
        }
    }
    handleAbort(streamId) {
        this.activeStreams.delete(streamId);
    }
    base64ToUint8Array(base64) {
        const binaryString = atob(base64);
        const bytes = new Uint8Array(binaryString.length);
        for (let i = 0; i < binaryString.length; i++) {
            bytes[i] = binaryString.charCodeAt(i);
        }
        return bytes;
    }
    streamToMediaElement(chunks, selector, contentType) {
        const element = document.querySelector(selector);
        if (!element) {
            console.error(`Media element not found: ${selector}`);
            return;
        }
        if (!(element instanceof HTMLVideoElement) && !(element instanceof HTMLAudioElement)) {
            console.error(`Element is not a video or audio element: ${selector}`);
            return;
        }
        // Combine all chunks into a single Blob
        const blob = new Blob(chunks, { type: contentType });
        // Create object URL and set as source
        const url = URL.createObjectURL(blob);
        element.src = url;
        // Cleanup URL when element is done playing or on error
        const cleanup = () => {
            URL.revokeObjectURL(url);
            element.removeEventListener('ended', cleanup);
            element.removeEventListener('error', cleanup);
        };
        element.addEventListener('ended', cleanup);
        element.addEventListener('error', cleanup);
        // Auto-play if desired (optional)
    }
    downloadFile(chunks, fileName, contentType) {
        // Combine all chunks into a single Blob
        const blob = new Blob(chunks, { type: contentType });
        // Create download link
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        // Trigger download
        document.body.appendChild(a);
        a.click();
        // Cleanup
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }
}
//# sourceMappingURL=StreamHandler.js.map