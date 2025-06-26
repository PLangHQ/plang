import React, { useRef } from "react";
import ReactDOM from "react-dom/client";
import { Tldraw, TldrawEditor, exportToBlob } from "@tldraw/tldraw";
import "@tldraw/tldraw/tldraw.css";

const App = () => {
    const editorRef = useRef(null);
    const blob = exportToBlob;
    return (
            <div style={{ width: "100vw", height: "80vh" }}>
                <Tldraw onMount={(editor) => {
                editorRef.current = editor;
                console.log(editor)
                window.tldrawEditor = editor;
                window.exportToBlob = exportToBlob;
            }} />
            </div>
    );
};

const rootElement = document.getElementById("tldraw");

if (rootElement) {
    const root = ReactDOM.createRoot(rootElement);
    root.render(<App />);
}
