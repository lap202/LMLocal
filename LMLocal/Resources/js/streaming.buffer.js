/**
* A small buffer that accumulates incoming text chunks and exposes a
* "visible" assembled text in controlled increments. 
**/
export class StreamingBuffer {
    constructor() {
        this._fullText = "";
        this._cursor = 0;
        this._visibleText = "";
    }

    append(fullText) {
        if (!fullText) return;
        if (fullText.length <= this._fullText.length) return;
        this._fullText = fullText;
    }

    readNext(wordsPerRead = 2) {
        if (this._cursor >= this._fullText.length) return this._visibleText;

        const remaining = this._fullText.slice(this._cursor);

        // Capture both whitespace and non-whitespace tokens so leading spaces are preserved.
        const tokens = remaining.match(/\s+|\S+/g) || [];

        let wordsTaken = 0;
        const tokensToTake = [];
        for (let i = 0; i < tokens.length && wordsTaken < wordsPerRead; i++) {
            const t = tokens[i];
            tokensToTake.push(t);
            if (!/^\s+$/.test(t)) wordsTaken++;
        }
        const chunk = tokensToTake.join('');

        this._visibleText += chunk;
        this._cursor += chunk.length;
        return this._visibleText;
    }

    getRemainingWordsCount() {
        const remaining = this._fullText.slice(this._cursor);
        const words = remaining.match(/\S+/g);
        return words ? words.length : 0;
    }

    flush() {
        if (this._cursor < this._fullText.length) {
            this._visibleText = this._fullText;
            this._cursor = this._fullText.length;
        }
        return this._visibleText;
    }

    reset() {
        this._fullText = "";
        this._cursor = 0;
        this._visibleText = "";
    }

    get visibleText() {
        return this._visibleText;
    }
}