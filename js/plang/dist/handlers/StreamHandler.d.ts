import type { KindHandler, HandleCtx } from "../net/ResponseDispatcher.js";
import type { StreamMessage } from "../types/messages.js";
export declare class StreamHandler implements KindHandler<"stream"> {
    kind: "stream";
    activeStreams: Map<string, {
        chunks: Uint8Array[];
        contentType: string;
        fileName: string;
        isMediaStream?: boolean;
        target?: string;
    }>;
    handle(message: StreamMessage, _ctx: HandleCtx): Promise<void>;
    private handleStart;
    private handleChunk;
    private handleEnd;
    private handleAbort;
    private base64ToUint8Array;
    private streamToMediaElement;
    private downloadFile;
}
