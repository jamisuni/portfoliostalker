/**
 * Fixed Compact Stock Chart Component for Blazor Integration
 * Creates a small line chart showing 20-day stock price history
 * Size: ~1.5cm high � 9cm long
 * 
 * FIXES:
 * - Handles identical values without getting stuck
 * - Prevents division by zero errors
 * - Ensures minimum scale range for flat data
 */
function TinyStockChart(containerId, stockData, options = {}) {
    const container = document.getElementById(containerId);
    if (!container) {
        console.error(`Container with ID "${containerId}" not found`);
        return;
    }

    // Default options
    const config = {
        width: 270, // ~9cm at 96dpi
        height: 45,  // ~1.5cm at 96dpi
        backgroundColor: '#ffffff',
        lineColor: '#2563eb',
        lineWidth: 1.5,
        purchaseLineColor: '#10b981',
        purchaseLineWidth: 1,
        purchaseLineDash: [3, 3],
        firstPurchaseLineColor: '#3b82f6',
        firstPurchaseLineWidth: 1,
        firstPurchaseLineDash: [3, 3],
        textColor: '#374151',
        textSize: 10,
        companyNameColor: '#6b7280',
        companyNameSize: 11,
        padding: { top: 8, right: 8, bottom: 8, left: 35 },
        minScaleRange: 0.1, // Minimum range to prevent flat lines
        ...options
    };

    // Validate stock data
    if (!stockData || !Array.isArray(stockData) || stockData.length === 0) {
        console.error('Invalid stock data provided');
        return;
    }

    // Create canvas
    const canvas = document.createElement('canvas');
    canvas.width = config.width;
    canvas.height = config.height;
    canvas.style.cssText = `
        width: ${config.width}px;
        height: ${config.height}px;
        background: ${config.backgroundColor};
        border: 1px solid #e5e7eb;
        border-radius: 4px;
    `;

    const ctx = canvas.getContext('2d');

    // Clear canvas
    ctx.fillStyle = config.backgroundColor;
    ctx.fillRect(0, 0, config.width, config.height);

    // Calculate data bounds with special handling for identical values
    const prices = stockData.map(d => d.price);
    const minPrice = Math.min(...prices);
    const maxPrice = Math.max(...prices);
    let priceRange = maxPrice - minPrice;

    // CRITICAL FIX: Handle identical values
    if (priceRange === 0 || priceRange < config.minScaleRange) {
        priceRange = Math.max(config.minScaleRange, minPrice * 0.1); // Use 10% of price or minimum range
    }

    // Calculate nice round numbers for scale
    const padding = priceRange * 0.1; // 10% padding
    let rawMin = minPrice - padding;
    let rawMax = maxPrice + padding;

    // For identical values, center the value and create symmetric range
    if (maxPrice === minPrice) {
        const halfRange = priceRange / 2;
        rawMin = minPrice - halfRange;
        rawMax = minPrice + halfRange;
    }

    // Helper function to round to nice numbers with proper decimal handling
    function getRoundedScale(min, max) {
        const range = max - min;

        // CRITICAL FIX: Prevent zero range
        if (range <= 0) {
            const centerValue = (min + max) / 2;
            const defaultRange = Math.max(config.minScaleRange, Math.abs(centerValue) * 0.1);
            return {
                min: centerValue - defaultRange / 2,
                max: centerValue + defaultRange / 2,
                middle: centerValue,
                step: defaultRange / 4
            };
        }

        const magnitude = Math.pow(10, Math.floor(Math.log10(range)));
        const normalizedRange = range / magnitude;

        let step;
        if (normalizedRange <= 1) step = 0.2 * magnitude;
        else if (normalizedRange <= 2) step = 0.5 * magnitude;
        else if (normalizedRange <= 5) step = 1 * magnitude;
        else step = 2 * magnitude;

        // For very small ranges, ensure we use smaller steps
        if (range < 2) {
            step = Math.max(step, 0.1);
        }
        if (range < 0.2) {
            step = Math.max(step, 0.01);
        }

        const roundedMin = Math.floor(min / step) * step;
        const roundedMax = Math.ceil(max / step) * step;

        // Ensure we have at least 3 distinct levels
        let adjustedMax = roundedMax;
        let adjustedMin = roundedMin;

        // If the range is too small, expand it
        while ((adjustedMax - adjustedMin) / step < 2) {
            if (step >= 1) {
                step = 0.5;
            } else if (step >= 0.5) {
                step = 0.25;
            } else if (step >= 0.25) {
                step = 0.1;
            } else if (step >= 0.1) {
                step = 0.05;
            } else {
                step = 0.01;
            }
            adjustedMin = Math.floor(min / step) * step;
            adjustedMax = Math.ceil(max / step) * step;
        }

        return {
            min: adjustedMin,
            max: adjustedMax,
            middle: (adjustedMin + adjustedMax) / 2,
            step: step
        };
    }

    const scale = getRoundedScale(rawMin, rawMax);
    const paddedMin = scale.min;
    const paddedMax = scale.max;

    // Chart area dimensions
    const chartWidth = config.width - config.padding.left - config.padding.right;
    const chartHeight = config.height - config.padding.top - config.padding.bottom;

    // Helper function to convert price to Y coordinate
    function priceToY(price) {
        const range = paddedMax - paddedMin;

        // CRITICAL FIX: Prevent division by zero
        if (range === 0) {
            return config.padding.top + chartHeight / 2; // Center line for flat data
        }

        return config.padding.top + chartHeight -
            ((price - paddedMin) / range) * chartHeight;
    }

    // Helper function to convert index to X coordinate
    function indexToX(index) {
        if (stockData.length <= 1) {
            return config.padding.left + chartWidth / 2; // Center for single point
        }
        return config.padding.left + (index / (stockData.length - 1)) * chartWidth;
    }

    // Draw company name in top area
    if (options.companyName) {
        ctx.fillStyle = config.companyNameColor;
        ctx.font = `${config.companyNameSize}px Arial`;
        ctx.textAlign = 'left';
        ctx.textBaseline = 'top';

        // Position company name in top-left of chart area
        const nameX = config.padding.left + 2;
        const nameY = config.padding.top + 1;

        // Draw with slight shadow for better visibility over chart lines
        ctx.fillStyle = 'rgba(255, 255, 255, 0.8)';
        ctx.fillText(options.companyName, nameX + 1, nameY + 1);
        ctx.fillStyle = config.companyNameColor;
        ctx.fillText(options.companyName, nameX, nameY);
    }

    // Draw Y-axis labels (3 scale numbers)
    ctx.fillStyle = config.textColor;
    ctx.font = `${config.textSize}px Arial`;
    ctx.textAlign = 'right';
    ctx.textBaseline = 'middle';

    // Format price for display - handle decimals properly
    function formatPrice(price) {
        if (Math.abs(price) >= 1000) {
            return (Math.round(price / 100) / 10) + 'k';
        } else {
            // Determine decimal places based on the step size
            let decimalPlaces = 0;
            if (scale.step < 1) {
                decimalPlaces = Math.max(0, -Math.floor(Math.log10(scale.step)));
            }

            // For very small steps, limit to 2 decimal places for readability
            decimalPlaces = Math.min(decimalPlaces, 2);

            return price.toFixed(decimalPlaces);
        }
    }

    // Draw scale labels - use rounded values
    ctx.fillText(formatPrice(scale.max), config.padding.left - 5, priceToY(scale.max));
    ctx.fillText(formatPrice(scale.middle), config.padding.left - 5, priceToY(scale.middle));
    ctx.fillText(formatPrice(scale.min), config.padding.left - 5, priceToY(scale.min));

    // Draw price line
    ctx.beginPath();
    ctx.strokeStyle = config.lineColor;
    ctx.lineWidth = config.lineWidth;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';

    stockData.forEach((data, index) => {
        const x = indexToX(index);
        const y = priceToY(data.price);

        if (index === 0) {
            ctx.moveTo(x, y);
        } else {
            ctx.lineTo(x, y);
        }
    });
    ctx.stroke();

    // Draw purchase price lines if provided
    if (options.purchasePrices && Array.isArray(options.purchasePrices)) {
        options.purchasePrices.forEach(purchasePrice => {
            // Only draw if purchase price is within visible range
            if (purchasePrice >= paddedMin && purchasePrice <= paddedMax) {
                const y = priceToY(purchasePrice);

                ctx.beginPath();
                ctx.strokeStyle = config.purchaseLineColor;
                ctx.lineWidth = config.purchaseLineWidth;
                ctx.setLineDash(config.purchaseLineDash);
                ctx.moveTo(config.padding.left, y);
                ctx.lineTo(config.width - config.padding.right, y);
                ctx.stroke();
                ctx.setLineDash([]); // Reset line dash
            }
        });
    }

    // Draw average purchase price line (green) if provided
    if (options.averagePurchasePrice &&
        options.averagePurchasePrice >= paddedMin &&
        options.averagePurchasePrice <= paddedMax) {

        const y = priceToY(options.averagePurchasePrice);

        ctx.beginPath();
        ctx.strokeStyle = config.purchaseLineColor;
        ctx.lineWidth = config.purchaseLineWidth;
        ctx.setLineDash(config.purchaseLineDash);
        ctx.moveTo(config.padding.left, y);
        ctx.lineTo(config.width - config.padding.right, y);
        ctx.stroke();
        ctx.setLineDash([]); // Reset line dash
    }

    // Draw first purchase price line (blue) if provided
    if (options.firstPurchasePrice &&
        options.firstPurchasePrice >= paddedMin &&
        options.firstPurchasePrice <= paddedMax) {

        const y = priceToY(options.firstPurchasePrice);

        ctx.beginPath();
        ctx.strokeStyle = config.firstPurchaseLineColor;
        ctx.lineWidth = config.firstPurchaseLineWidth;
        ctx.setLineDash(config.firstPurchaseLineDash);
        ctx.moveTo(config.padding.left, y);
        ctx.lineTo(config.width - config.padding.right, y);
        ctx.stroke();
        ctx.setLineDash([]); // Reset line dash
    }

    // Clear container and add canvas
    container.innerHTML = '';
    container.appendChild(canvas);

    return {
        canvas: canvas,
        update: function (newStockData, newOptions = {}) {
            createStockChart(containerId, newStockData, { ...options, ...newOptions });
        }
    };
}
