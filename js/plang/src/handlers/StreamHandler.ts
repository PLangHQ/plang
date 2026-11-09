import type { KindHandler, HandleCtx } from "../net/ResponseDispatcher.js";
import type { StreamMessage } from "../types/messages.js";

export class StreamHandler implements KindHandler<"stream"> {
    kind = "stream" as const;
    
    activeStreams = new Map<string, {
        chunks: Uint8Array[];
        contentType: string;
        fileName: string;
        isMediaStream?: boolean;
        target?: string;
    }>();
    
    async handle(message: StreamMessage, _ctx: HandleCtx) {
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
    
    private handleStart(
        streamId: string, 
        contentType: string | null | undefined, 
        fileName: string | null | undefined,
        actions?: string[],
        target?: string | null
    ): void {
        const isDownload = actions?.includes('download');
        const isStream = actions?.includes('stream');
        
        if (isDownload && fileName && contentType) {
            // File download
            this.activeStreams.set(streamId, {
                chunks: [],
                contentType: contentType,
                fileName: fileName
            });
        } else if (isStream && target && contentType) {
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
    
    private handleChunk(streamId: string, bytes: string | null | undefined): void {
        const stream = this.activeStreams.get(streamId);
        if (stream && bytes) {
            // bytes comes as base64 string from JSON
            const binaryData = this.base64ToUint8Array(bytes);
            stream.chunks.push(binaryData);
        }
    }
    
    private handleEnd(streamId: string): void {
        const stream = this.activeStreams.get(streamId);
        if (stream) {
            if (stream.isMediaStream && stream.target) {
                // Stream to media element
                this.streamToMediaElement(stream.chunks, stream.target, stream.contentType);
            } else {
                // Trigger download
                this.downloadFile(stream.chunks, stream.fileName, stream.contentType);
            }
            this.activeStreams.delete(streamId);
        }
    }
    
    private handleAbort(streamId: string): void {
        this.activeStreams.delete(streamId);
    }
    
    private base64ToUint8Array(base64: string): Uint8Array {
        const binaryString = atob(base64);
        const bytes = new Uint8Array(binaryString.length);
        for (let i = 0; i < binaryString.length; i++) {
            bytes[i] = binaryString.charCodeAt(i);
        }
        return bytes;
    }
    
    private streamToMediaElement(chunks: Uint8Array[], selector: string, contentType: string): void {
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
        const blob = new Blob(chunks as BlobPart[], { type: contentType });
        
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
    
    private downloadFile(chunks: Uint8Array[], fileName: string, contentType: string): void {
        // Combine all chunks into a single Blob
        const blob = new Blob(chunks as BlobPart[], { type: contentType });
        
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